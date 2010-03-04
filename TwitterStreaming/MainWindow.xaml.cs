/*
 * Copyright (C) 2010 Kazuki Oikawa
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ktwt.Json;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class MainWindow : Window
	{
		TwitterAccountManager _mgr;
		RootTimeLines _rootTLs = new RootTimeLines ();
		ObservableCollection<string> _hashTags = new ObservableCollection<string> ();

		Status _replyInfo;
		string _replyName;

		public MainWindow ()
		{
			_hashTags.Add (string.Empty);
			InitializeComponent ();
			itemsControl.DataContext = this;
			_mgr = new TwitterAccountManager ();
			IStreamingHandler[] targets;
			_mgr.Load (LoadConfig, out targets);

			postAccount.ItemsSource = _mgr.Accounts;
			if (_mgr.Accounts.Length > 0)
				postAccount.SelectedIndex = 0;

			// Setup streaming & friends/followers list
			if (targets != null) {
				ThreadPool.QueueUserWorkItem (delegate (object o) {
					TwitterAccount[] accounts = _mgr.Accounts;
					for (int i = 0; i < accounts.Length; i++) {
						try {
							accounts[i].TwitterClient.UpdateFriendIDs ();
						} catch {}
					}

					_mgr.ReconstructAllStreaming (targets, false);
					Dispatcher.Invoke (new EmptyDelegate (delegate () {
						foreach (TimelineInfo info in GetAllTimeLineInfo ())
							info.UpdateStreamingConstruction ();
					}));

					for (int i = 0; i < accounts.Length; i++) {
						try {
							accounts[i].TwitterClient.UpdateFriends ();
						} catch {}
						try {
							accounts[i].TwitterClient.UpdateFollowers ();
						} catch {}
					}

					Dispatcher.Invoke (new EmptyDelegate (delegate () {
						Binding binding = new Binding ("SelectedItem.TwitterClient.Friends");
						binding.ElementName = "postAccount";
						BindingOperations.SetBinding (cbDMto, ComboBox.ItemsSourceProperty, binding);
					}));
				});
			}
		}

		#region Config Load/Save
		void LoadConfig (JsonObject root)
		{
			if (root.Value.ContainsKey ("windows"))
				LoadConfigInternal ((JsonArray)root.Value["windows"], _rootTLs);
			if (root.Value.ContainsKey ("colors")) // compatibility 0.0.5
				LoadConfigInternalColors ((JsonObject)root.Value["colors"]);
			if (root.Value.ContainsKey ("fonts"))  // compatibility 0.0.5
				LoadConfigInternalFonts ((JsonObject)root.Value["fonts"]);
			if (root.Value.ContainsKey ("hashTags"))
				LoadConfigInternalHashTags ((JsonArray)root.Value["hashTags"]);
			if (root.Value.ContainsKey ("footer"))
				FooterText = (root.Value["footer"] as JsonString).Value;

			if (root.Value.ContainsKey ("streaming")) {
				JsonObject conf = (JsonObject)root.Value["streaming"];
				if (conf.Value.ContainsKey ("include_other"))
					IsIncludeOtherStatus = (conf.Value["include_other"] as JsonBoolean).Value;
			}
			if (root.Value.ContainsKey ("styles"))
				LoadConfigInternalStyles ((JsonObject)root.Value["styles"]);
		}
		void LoadConfigInternal (JsonArray array, TimelineBase timelines)
		{
			for (int i = 0; i < array.Length; i ++) {
				JsonObject obj = array[i] as JsonObject;
				if (obj != null) {
					TimelineBase info = null;
					switch ((obj.Value["type"] as JsonString).Value) {
						case "account":
							string subtype = (obj.Value["subtype"] as JsonString).Value;
							string name = (obj.Value["name"] as JsonString).Value;
							TwitterAccount account = null;
							foreach (TwitterAccount item in _mgr.Accounts)
								if (name.Equals (item.ScreenName)) {
									account = item;
									break;
								}
							if (account == null) continue;
							switch (subtype) {
								case "home": info = new TimelineInfo (timelines, account, account.HomeTimeline); break;
								case "mentions": info = new TimelineInfo (timelines, account, account.Mentions); break;
								case "directmessages": info = new TimelineInfo (timelines, account, account.DirectMessages); break;
							}
							break;
						case "search":
							string keywords = (obj.Value["keywords"] as JsonString).Value;
							foreach (SearchStatuses search in _mgr.Searches)
								if (keywords.Equals (search.Keyword)) {
									info = new TimelineInfo (timelines, search);
									break;
								}
							break;
						case "tab":
							string title = (obj.Value["title"] as JsonString).Value;
							TabInfo tb = new TabInfo (timelines, title);
							LoadConfigInternal ((JsonArray)obj.Value["windows"], tb);
							info = tb;
							break;
					}
					if (info != null)
						timelines.TimeLines.Add (info);
				}
			}
		}
		void LoadConfigInternalStyles (JsonObject o)
		{
			if (o.Value.ContainsKey ("colors"))
				LoadConfigInternalColors ((JsonObject)o.Value["colors"]);
			if (o.Value.ContainsKey ("fonts"))
				LoadConfigInternalFonts ((JsonObject)o.Value["fonts"]);
			if (o.Value.ContainsKey ("icon_size"))
				IconSize = (int)(o.Value["icon_size"] as JsonNumber).Value;
		}
		void LoadConfigInternalColors (JsonObject o)
		{
			ColorCodeNameConverter conv = new ColorCodeNameConverter ();
			Background = LoadConfigInternalColors (o, "bg", conv, Background);
			Foreground = LoadConfigInternalColors (o, "fg", conv, Foreground);
			PostTextBox.Background = LoadConfigInternalColors (o, "postTextBoxBg", conv, PostTextBox.Background);
			PostTextBox.Foreground = LoadConfigInternalColors (o, "postTextBoxFg", conv, PostTextBox.Foreground);
			PostBackground = LoadConfigInternalColors (o, "postBg", conv, PostBackground);
			PostForeground = LoadConfigInternalColors (o, "postFg", conv, PostForeground);
			NameForeground = LoadConfigInternalColors (o, "postNameFg", conv, NameForeground);
			LinkForeground = LoadConfigInternalColors (o, "postLinkFg", conv, LinkForeground);
		}
		Brush LoadConfigInternalColors (JsonObject o, string key, ColorCodeNameConverter conv, Brush def)
		{
			try {
				if (!o.Value.ContainsKey (key))
					return def;
				return (Brush)conv.ConvertBack ((o.Value[key] as JsonString).Value, null, null, null);
			} catch {
				return def;
			}
		}
		void LoadConfigInternalFonts (JsonObject o)
		{
			try {
				FontFamily = new FontFamily ((o.Value["main-family"] as JsonString).Value);
			} catch {}
			FontSize = (o.Value["main-size"] as JsonNumber).Value;
		}
		void LoadConfigInternalHashTags (JsonArray array)
		{
			for (int i = 0; i < array.Length; i ++)
				_hashTags.Add ((array[i] as JsonString).Value);
		}

		void SaveConfig ()
		{
			_mgr.Save (delegate (JsonTextWriter writer) {
				writer.WriteKey ("windows");
				writer.WriteStartArray ();
				SaveConfigInternal (writer, _rootTLs);
				writer.WriteEndArray ();

				writer.WriteKey ("styles");
				SaveConfigInternalStyles (writer);

				writer.WriteKey ("hashTags");
				SaveConfigInternalHashTags (writer);

				writer.WriteKey ("footer");
				writer.WriteString (FooterText);

				writer.WriteKey ("streaming");
				writer.WriteStartObject ();
				writer.WriteKey ("include_other");
				writer.WriteBoolean (IsIncludeOtherStatus);
				writer.WriteEndObject ();
			});
		}
		void SaveConfigInternal (JsonTextWriter writer, TimelineBase timelines)
		{
			foreach (object item in timelines.TimeLines) {
				writer.WriteStartObject ();
				writer.WriteKey ("type");
				TimelineInfo tl = item as TimelineInfo;
				TabInfo tb = item as TabInfo;
				if (tl != null) {
					if (tl.Search == null) {
						writer.WriteString ("account");
						writer.WriteKey ("subtype");
						if (tl.Statuses == tl.RestAccount.HomeTimeline)
							writer.WriteString ("home");
						else if (tl.Statuses == tl.RestAccount.Mentions)
							writer.WriteString ("mentions");
						else if (tl.Statuses == tl.RestAccount.DirectMessages)
							writer.WriteString ("directmessages");
						writer.WriteKey ("name");
						writer.WriteString (tl.RestAccount.ScreenName);
					} else {
						writer.WriteString ("search");
						writer.WriteKey ("keywords");
						writer.WriteString (tl.Search.Keyword);
					}
				} else if (tb != null) {
					writer.WriteString ("tab");
					writer.WriteKey ("title");
					writer.WriteString (tb.Title);
					writer.WriteKey ("windows");
					writer.WriteStartArray ();
					SaveConfigInternal (writer, tb);
					writer.WriteEndArray ();
				} else {
					writer.WriteNull ();
				}
				writer.WriteEndObject ();
			}
		}
		void SaveConfigInternalStyles (JsonTextWriter writer)
		{
			writer.WriteStartObject ();

			writer.WriteKey ("colors");
			writer.WriteStartObject ();
			SaveConfigInternalColors (writer);
			writer.WriteEndObject ();

			writer.WriteKey ("fonts");
			writer.WriteStartObject ();
			SaveConfigInternalFonts (writer);
			writer.WriteEndObject ();

			writer.WriteKey ("icon_size");
			writer.WriteNumber (IconSize);

			writer.WriteEndObject ();
		}
		void SaveConfigInternalColors (JsonTextWriter writer)
		{
			ColorCodeNameConverter conv = new ColorCodeNameConverter ();
			writer.WriteKey ("bg");
			writer.WriteString ((string)conv.Convert (Background, null, null, null));
			writer.WriteKey ("fg");
			writer.WriteString ((string)conv.Convert (Foreground, null, null, null));
			writer.WriteKey ("postTextBoxBg");
			writer.WriteString ((string)conv.Convert (PostTextBox.Background, null, null, null));
			writer.WriteKey ("postTextBoxFg");
			writer.WriteString ((string)conv.Convert (PostTextBox.Foreground, null, null, null));
			writer.WriteKey ("postBg");
			writer.WriteString ((string)conv.Convert (PostBackground, null, null, null));
			writer.WriteKey ("postFg");
			writer.WriteString ((string)conv.Convert (PostForeground, null, null, null));
			writer.WriteKey ("postNameFg");
			writer.WriteString ((string)conv.Convert (NameForeground, null, null, null));
			writer.WriteKey ("postLinkFg");
			writer.WriteString ((string)conv.Convert (LinkForeground, null, null, null));
		}
		void SaveConfigInternalFonts (JsonTextWriter writer)
		{
			writer.WriteKey ("main-family");
			writer.WriteString (FontFamily.ToString ());
			writer.WriteKey ("main-size");
			writer.WriteNumber (FontSize);
		}
		void SaveConfigInternalHashTags (JsonTextWriter writer)
		{
			writer.WriteStartArray ();
			for (int i = 1; i < _hashTags.Count; i ++)
				writer.WriteString (_hashTags[i]);
			writer.WriteEndArray ();
		}
		#endregion

		public TextBox PostTextBox {
			get { return postTextBox; }
		}

		public ObservableCollection<TimelineBase> TimeLines {
			get { return _rootTLs.TimeLines; }
		}

		List<TimelineInfo> GetAllTimeLineInfo ()
		{
			return GetAllChildrenTimeLineInfo (_rootTLs);
		}

		List<TimelineInfo> GetAllChildrenTimeLineInfo (TimelineBase root)
		{
			Queue<TimelineBase> queue = new Queue<TimelineBase> ();
			List<TimelineInfo> list = new List<TimelineInfo> ();
			queue.Enqueue (root);
			while (queue.Count > 0) {
				TimelineBase tl = queue.Dequeue ();
				if (tl.TimeLines != null) {
					for (int i = 0; i < tl.TimeLines.Count; i++)
						queue.Enqueue (tl.TimeLines[i]);
				}
				if (tl is TimelineInfo)
					list.Add ((TimelineInfo)tl);
			}
			return list;
		}

		bool UseTimeline (TwitterTimeLine tl)
		{
			return UseTimeline (tl, GetAllTimeLineInfo ());
		}

		bool UseTimeline (TwitterTimeLine tl, List<TimelineInfo> list)
		{
			foreach (TimelineInfo info in list) {
				if (info != null) {
					if (info.Statuses == tl)
						return true;
				}
			}
			return false;
		}

		private void TimeLineCloseButton_Click (object sender, RoutedEventArgs e)
		{
			TimelineInfo tl = (sender as Button).DataContext as TimelineInfo;
			TabInfo tb = (sender as Button).DataContext as TabInfo;
			if (tl != null && MessageBox.Show (tl.Title + " を閉じてもよろしいですか?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
				tl.Owner.TimeLines.Remove (tl);
				if (!UseTimeline (tl.Statuses))
					_mgr.CloseTimeLine (tl.Statuses);
			} else if (tb != null && MessageBox.Show ("タブコンテナ " + tb.Title + " を閉じてもよろしいですか?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
				tb.Owner.TimeLines.Remove (tb);
				List<TimelineInfo> list = GetAllChildrenTimeLineInfo (tb);
				List<TimelineInfo> all = GetAllTimeLineInfo ();
				for (int i = 0; i < list.Count; i ++) {
					if (!UseTimeline (list[i].Statuses, all))
						_mgr.CloseTimeLine (list[i].Statuses);
				}
			}
			SaveConfig ();
		}

		private void TimeLineMoveLeftButton_Click (object sender, RoutedEventArgs e)
		{
			TimelineBase sel = (sender as Button).DataContext as TimelineBase;
			if (sel == null || sel.Owner == null || sel.Owner.TimeLines == null)
				return;
			int idx = sel.Owner.TimeLines.IndexOf (sel);
			if (sel.Owner.TimeLines.Count > 1 && idx > 0) {
				TabInfo tabInfo = sel.Owner.TimeLines[idx - 1] as TabInfo;
				if (tabInfo != null) {
					tabInfo.Add (sel);
				} else {
					sel.Owner.Move (idx, idx - 1);
				}
			} else {
				TabInfo tabInfo = sel.Owner as TabInfo;
				if (tabInfo != null) {
					idx = tabInfo.Owner.TimeLines.IndexOf (tabInfo);
					tabInfo.Owner.Insert (idx, sel);
				}
			}
			SaveConfig ();
		}

		private void TimeLineMoveRightButton_Click (object sender, RoutedEventArgs e)
		{
			TimelineBase sel = (sender as Button).DataContext as TimelineBase;
			if (sel == null || sel.Owner == null || sel.Owner.TimeLines == null)
				return;
			int idx = sel.Owner.TimeLines.IndexOf (sel);
			if (idx < 0) return;
			if (sel.Owner.TimeLines.Count > 1 && idx < sel.Owner.TimeLines.Count - 1) {
				TabInfo tabInfo = sel.Owner.TimeLines[idx + 1] as TabInfo;
				if (tabInfo != null) {
					tabInfo.Insert (0, sel);
				} else {
					sel.Owner.Move (idx, idx + 1);
				}
			} else {
				TabInfo tabInfo = sel.Owner as TabInfo;
				if (tabInfo != null) {
					idx = tabInfo.Owner.TimeLines.IndexOf (tabInfo);
					tabInfo.Owner.Insert (idx + 1, sel);
				}
			}
			SaveConfig ();
		}

		private void TwitterStatusViewer_LinkClick (object sender, LinkClickEventArgs e)
		{
			string url;
			switch (e.Url[0]) {
				case '@':
					url = "https://twitter.com/" + e.Url.Substring (1);
					break;
				case '#':
					url = "https://search.twitter.com/search?q=" + Uri.EscapeUriString (e.Url.Substring (1));
					break;
				case '/':
					url = "https://twitter.com" + e.Url;
					break;
				default:
					url = e.Url;
					break;
			}

			try {
				Process.Start (url);
			} catch {}
		}

		private void TwitterStatusViewer_MouseDoubleClick (object sender, MouseButtonEventArgs e)
		{
			Status selected = sender as Status;
			if (selected == null)
				selected = (sender as TwitterStatusViewer).Status;
			if (selected.ID == 0 || selected.User.ScreenName == null)
				return;

			_replyInfo = selected;
			_replyName = "@" + selected.User.ScreenName;
			postTextBox.Text = _replyName + " ";
			SetReplySetting ();
			Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				postTextBox.SelectionStart = postTextBox.Text.Length;
				postTextBox.Focus ();
			}));
		}

		private void TwitterStatusViewer_FavoriteIconClick (object sender, RoutedEventArgs e)
		{
			Status selected = sender as Status;
			TwitterAccount account = selected.AccountInfo as TwitterAccount;
			if (account == null) {
				selected.IsFavorited = selected.IsFavorited;
				return;
			}
			Favorite (account, selected, !selected.IsFavorited);
		}

		void Favorite (TwitterAccount account, Status status, bool isFavorite)
		{
			ThreadPool.QueueUserWorkItem (delegate (object o) {
				bool new_fav = isFavorite;
				try {
					if (isFavorite) {
						account.TwitterClient.FavoritesCreate (status.ID);
					} else {
						account.TwitterClient.FavoritesDestroy (status.ID);
					}
				} catch {
					new_fav = !isFavorite;
				}
				try {
					new_fav = account.TwitterClient.Show (status.ID).IsFavorited;
				} catch {}
				Dispatcher.Invoke (new EmptyDelegate (delegate () {
					status.IsFavorited = new_fav;
				}));
			});
		}

		private void postTextBox_KeyDown (object sender, KeyEventArgs e)
		{
			if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Return)
				postButton_Click (null, null);
		}

		private void postTextBox_TextChanged (object sender, TextChangedEventArgs e)
		{
			int diff = TwitterClient.MaxStatusLength - postTextBox.Text.Length;
			postLengthText.Text = diff.ToString ();
			postLengthText.Foreground = (diff < 0 ? Brushes.Red : Brushes.White);
			if (cbDMto.SelectedIndex < 0) {
				if (_replyInfo != null) {
					if (CheckReplyText (postTextBox.Text))
						SetReplySetting ();
					else
						ResetReplySetting (true);
				}
			}
		}

		private void postButton_Click (object sender, RoutedEventArgs e)
		{
			string txt = postTextBox.Text.Trim ();
			if (txt.Length == 0) return;
			postTextBox.IsReadOnly = true;
			postTextBox.Foreground = Brushes.DimGray;
			postButton.IsEnabled = false;
			if (cbDMto.SelectedIndex < 0) {
				if (!CheckReplyText (txt) || (_replyName != null && _replyName[1] == 'R'))
					ResetReplySetting (false);
			}
			ThreadPool.QueueUserWorkItem (PostProcess, new object[] {
				txt,
				postAccount.SelectedItem,
				cbDMto.SelectedItem
			});
		}

		private void ClearButton_Click (object sender, RoutedEventArgs e)
		{
			ResetReplySetting (false);
			ResetPostTextBox ();
		}

		void PostProcess (object o)
		{
			object[] items = (object[])o;
			string txt = (string)items[0];
			TwitterAccount account = (TwitterAccount)items[1];
			User dmTo = items[2] as User;
			Status status = null;
			try {
				if (dmTo == null) {
					status = account.TwitterClient.Update (txt, (_replyInfo == null ? (ulong?)null : _replyInfo.ID), null, null);
				} else {
					status = account.TwitterClient.SendDirectMessage (null, dmTo.ID, txt);
				}
			} catch {}
			Dispatcher.Invoke (new EmptyDelegate (delegate () {
				postTextBox.IsReadOnly = false;
				postTextBox.Foreground = Brushes.White;
				postButton.IsEnabled = true;
				if (status != null) {
					ResetReplySetting (false);
					ResetPostTextBox ();
					if (dmTo == null) {
						account.HomeTimeline.Add (status);
					} else {
						account.DirectMessages.Add (status);
					}
				}
			}));
		}

		bool CheckReplyText (string txt)
		{
			if (_replyInfo == null || _replyName == null || _replyName.Length == 0)
				return false;
			if (_replyName[0] == '@' && txt.StartsWith (_replyName))
				return true; // reply
			if (_replyName[0] == 'Q' && txt.Contains (_replyName))
				return true; // QT
			if (_replyName[0] == 'R' && txt.Contains (_replyName))
				return true; // Unofficial RT
			return false;
		}

		void ResetPostTextBox ()
		{
			cbDMto.SelectedIndex = -1;
			postTextBox.Text = GenerateFooter ();
			postTextBox.SelectionStart = 0;
			postTextBox.Focus ();
		}

		void SetReplySetting ()
		{
			postButton.Content = (_replyName[0] == '@' ? "Reply" :
				_replyName[0] == 'Q' ? " QT " : "Retweet");
		}

		void ResetReplySetting (bool btnTextOnly)
		{
			if (!btnTextOnly) {
				_replyInfo = null;
				_replyName = null;
			}
			postButton.Content = "Post";
		}

		private void cbDMto_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			if (cbDMto.SelectedIndex >= 0)
				postButton.Content = "Send DirectMessage";
		}

		#region Streaming Filter
		public static readonly DependencyProperty IsIncludeOtherStatusProperty =
			DependencyProperty.Register ("IsIncludeOtherStatus", typeof (bool), typeof (MainWindow), new PropertyMetadata (false, IsIncludeOtherStatus_Changed));
		public bool IsIncludeOtherStatus {
			get { return (bool)GetValue (IsIncludeOtherStatusProperty); }
			set { SetValue (IsIncludeOtherStatusProperty, value); }
		}
		static void IsIncludeOtherStatus_Changed (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			MainWindow self = (MainWindow)d;
			bool newValue = (bool)e.NewValue;
			for (int i = 0; i < self._mgr.Accounts.Length; i ++)
				self._mgr.Accounts[i].IsIncludeOtherStatus = newValue;
		}
		#endregion

		#region Menu Handlers
		private void MenuItem_AddNewTimeline_Click (object sender, RoutedEventArgs e)
		{
			NewTimelineWindow win = new NewTimelineWindow (_mgr);
			win.Owner = this;
			bool? ret = win.ShowDialog ();
			if (!ret.HasValue || !ret.Value)
				return;

			TimelineBase info = null;
			TwitterAccount account = win.SelectedAccount;
			if (win.IsCheckedAccountTimeline) {
				info = new TimelineInfo (_rootTLs, account, win.SelectedAccountTimeline);
			} else if (win.IsCheckedNewSearch && win.SearchKeyword.Length > 0) {
				SearchStatuses search = new SearchStatuses (account, win.SearchKeyword);
				if (win.IsUseStreamingForSearch)
					search.StreamingClient = new StreamingClient (new TwitterAccount[] {account}, search.Keyword, search, false);
				_mgr.AddSearchInfo (search);
				info = new TimelineInfo (_rootTLs, search);
			} else if (win.IsCheckedExistedSearch && win.SelectedExistedSearch != null) {
				info = new TimelineInfo (_rootTLs, win.SelectedExistedSearch);
			} else if (win.IsCheckedNewTab && win.NewTabTitle.Length > 0) {
				info = new TabInfo (_rootTLs, win.NewTabTitle);
			}
			if (info != null) {
				_rootTLs.TimeLines.Add (info);
				SaveConfig ();
			}
		}

		private void MenuItem_ShowPreference_Click (object sender, RoutedEventArgs e)
		{
			PreferenceWindow win = new PreferenceWindow (_mgr, this);
			win.Owner = this;
			win.ShowDialog ();
			if (win.IsAccountArrayChanged) {
				_mgr.UpdateAccounts (win.Accounts);
				postAccount.ItemsSource = _mgr.Accounts;
			}

			if (win.IsStreamingTargetsChanged) {
				_mgr.ReconstructAllStreaming (win.StreamingTargets, false);
				foreach (TimelineInfo info in GetAllTimeLineInfo ())
					info.UpdateStreamingConstruction ();
			}

			SaveConfig ();
		}

		private void MenuItem_ShowFriendsFollowers_Click (object sender, RoutedEventArgs e)
		{
			FriendsManageWindow win = new FriendsManageWindow (_mgr);
			win.Owner = this;
			win.ShowDialog ();
		}

		private void MenuItem_ShowAboutWindow_Click (object sender, RoutedEventArgs e)
		{
			AboutWindow win = new AboutWindow ();
			win.Owner = this;
			win.ShowDialog ();
		}

		private void MenuItem_UpdateFriendsAndFollowers_Click (object sender, RoutedEventArgs e)
		{
			for (int i = 0; i < _mgr.Accounts.Length; i ++) {
				ThreadPool.QueueUserWorkItem (delegate (object o) {
					TwitterAccount account = o as TwitterAccount;
					try {
						account.TwitterClient.UpdateFriends ();
					} catch {}
					try {
						account.TwitterClient.UpdateFollowers ();
					} catch {}
				}, _mgr.Accounts[i]);
			}
		}

		private void MenuItem_OpenUrl_Click (object sender, RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			if (item == null || item.Tag == null || !(item.Tag is string))
				return;
			string url = (string)item.Tag;
			try {
				Process.Start (url);
			} catch {}
		}

		private void ReplyMenuItem_Click (object sender, RoutedEventArgs e)
		{
			ListBox lb = (ListBox)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget;
			if (lb.SelectedItem == null) return;
			TwitterStatusViewer_MouseDoubleClick (lb.SelectedItem, null);
		}

		private void RetweetMenuItem_Click (object sender, RoutedEventArgs e)
		{
			ListBox lb = (ListBox)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget;
			Status status = lb.SelectedItem as Status;
			if (status == null) return;
			if (MessageBox.Show (string.Format ("以下の投稿をRetweetしますか？\r\n{0}: {1}", status.User.ScreenName, status.Text), string.Empty, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
				return;
			TwitterAccount account = (lb.DataContext as TimelineInfo).RestAccount;
			ThreadPool.QueueUserWorkItem (delegate (object o) {
				bool retry = true;
				while (retry) {
					try {
						Status retweeted = account.TwitterClient.Retweet (status.ID);
						Dispatcher.Invoke (new EmptyDelegate (delegate () {
							account.HomeTimeline.Add (retweeted);
						}));
						return;
					} catch {
						Dispatcher.Invoke (new EmptyDelegate (delegate () {
							if (MessageBox.Show (string.Format ("以下の投稿のRetweetに失敗しました。再試行しますか？\r\n{0}: {1}", status.User.ScreenName, status.Text), string.Empty, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
								retry = false;
						}));
					}
				}
			});
		}

		private void QuotedTweetMenuItem_Click (object sender, RoutedEventArgs e)
		{
			Status status = ((ListBox)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget).SelectedItem as Status;

			_replyInfo = status;
			_replyName = "QT @" + status.User.ScreenName;
			postTextBox.Text = " " + _replyName + ": " + status.Text;
			SetReplySetting ();
			Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				postTextBox.SelectionStart = 0;
				postTextBox.Focus ();
			}));
		}

		private void BadRetweetMenuItem_Click (object sender, RoutedEventArgs e)
		{
			Status status = ((ListBox)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget).SelectedItem as Status;

			_replyInfo = status;
			_replyName = "RT @" + status.User.ScreenName;
			postTextBox.Text = _replyName + ": " + status.Text;
			SetReplySetting ();
			Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				postTextBox.SelectionStart = 0;
				postTextBox.Focus ();
			}));
		}

		private void CopyMenuItem_Click (object sender, RoutedEventArgs e)
		{
			string tag = (sender as MenuItem).Tag as string;
			Status status = ((ListBox)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget).SelectedItem as Status;
			if (status == null || tag == null)
				return;
			string txt = null;
			switch (tag) {
				case "ScreenName": txt = status.User.ScreenName; break;
				case "Name": txt = status.User.Name; break;
				case "Text": txt = status.Text; break;
			}
			if (txt == null) return;
			for (int i = 0; i < 3; i ++) {
				try {
					Clipboard.SetText (txt);
					return;
				} catch {}
				Thread.Sleep (0);
			}
			MessageBox.Show ("クリップボードにアクセスできなかったため，コピーできませんでした．");
		}

		private void OpenLinkMenuItem_Click (object sender, RoutedEventArgs e)
		{
			string tag = (sender as MenuItem).Tag as string;
			Status status = ((ListBox)((ContextMenu)((MenuItem)sender).Parent).PlacementTarget).SelectedItem as Status;
			if (status == null || tag == null)
				return;
			string url = null;
			switch (tag) {
				case "Permalink": url = string.Format ("/{0}/status/{1}", status.User.ScreenName, status.ID); break;
				case "User": url = "/" + status.User.ScreenName; break;
			}
			if (url == null) return;
			url = "https://twitter.com" + url;
			try {
				Process.Start (url);
			} catch {}
		}
		#endregion

		#region Colors
		public static readonly DependencyProperty PostBackgroundProperty =
			DependencyProperty.Register ("PostBackground", typeof (Brush), typeof (MainWindow), new FrameworkPropertyMetadata (new SolidColorBrush (Color.FromRgb (0x33, 0x33, 0x33)), FrameworkPropertyMetadataOptions.AffectsRender));
		public Brush PostBackground {
			get { return (Brush)GetValue (PostBackgroundProperty); }
			set { SetValue (PostBackgroundProperty, value); }
		}

		public static readonly DependencyProperty PostForegroundProperty =
			DependencyProperty.Register ("PostForeground", typeof (Brush), typeof (MainWindow), new FrameworkPropertyMetadata (Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
		public Brush PostForeground {
			get { return (Brush)GetValue (PostForegroundProperty); }
			set { SetValue (PostForegroundProperty, value); }
		}

		public static readonly DependencyProperty NameForegroundProperty =
			DependencyProperty.Register ("NameForeground", typeof (Brush), typeof (MainWindow), new FrameworkPropertyMetadata (new SolidColorBrush (Color.FromRgb (0x77, 0x77, 0xff)), FrameworkPropertyMetadataOptions.AffectsRender));
		public Brush NameForeground {
			get { return (Brush)GetValue (NameForegroundProperty); }
			set { SetValue (NameForegroundProperty, value); }
		}

		public static readonly DependencyProperty LinkForegroundProperty =
			DependencyProperty.Register ("LinkForeground", typeof (Brush), typeof (MainWindow), new FrameworkPropertyMetadata (Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
		public Brush LinkForeground {
			get { return (Brush)GetValue (LinkForegroundProperty); }
			set { SetValue (LinkForegroundProperty, value); }
		}
		#endregion

		#region Hashtag / footer
		public ObservableCollection<string> HashTagList {
			get { return _hashTags; }
		}

		public static readonly DependencyProperty FooterTextProperty = DependencyProperty.Register ("FooterText", typeof (string), typeof (MainWindow), new FrameworkPropertyMetadata (string.Empty, FrameworkPropertyMetadataOptions.None));
		public string FooterText {
			get { return (string)GetValue (FooterTextProperty); }
			set { SetValue (FooterTextProperty, value); }
		}

		string GenerateFooter ()
		{
			string footer = FooterText;
			if (footer == null)
				footer = string.Empty;
			else if (footer.Length > 0)
				footer = " " + footer;
			string tag = cbHashTag.SelectedItem as string;
			if (tag != null && tag.Length > 0)
				footer = " " + tag + footer;
			return footer;
		}

		private void cbHashTag_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			string txt = postTextBox.Text;
			if (txt.IndexOf (" #") >= 0)
				txt = txt.Substring (0, txt.IndexOf (" #")).TrimEnd ();
			else if (FooterText != null && txt.EndsWith (FooterText))
				txt = txt.Substring (0, txt.Length - FooterText.Length).TrimEnd ();
			postTextBox.Text = txt + GenerateFooter ();
			postTextBox.SelectionStart = txt.Length;
			Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				postTextBox.Focus ();
			}), null);
		}
		#endregion

		#region Icon
		public static readonly DependencyProperty IconSizeProperty =
			DependencyProperty.Register ("IconSize", typeof (int), typeof (MainWindow), new PropertyMetadata (32));
		public int IconSize {
			get { return (int)GetValue (IconSizeProperty); }
			set { SetValue (IconSizeProperty, value); }
		}
		#endregion
	}

	public abstract class TimelineBase
	{
		protected TimelineBase (TimelineBase owner)
		{
			Owner = owner;
			TimeLines = null;
		}

		public virtual void Add (TimelineBase tl)
		{
			Insert (TimeLines.Count, tl);
		}
		public virtual void Insert (int idx, TimelineBase tl)
		{
			if (tl.Owner != null && tl.Owner.TimeLines != null)
				tl.Owner.Remove (tl);
			tl.Owner = this;
			TimeLines.Insert (idx, tl);
		}
		public virtual void Move (int oldIndex, int newIndex)
		{
			TimeLines.Move (oldIndex, newIndex);
		}
		public virtual void Remove (TimelineBase tl)
		{
			TimeLines.Remove (tl);
		}
		public virtual void RemoveAt (int idx)
		{
			TimeLines.RemoveAt (idx);
		}

		public TimelineBase Owner { get; set; }
		public ObservableCollection<TimelineBase> TimeLines { get; protected set; }
	}

	public class RootTimeLines : TimelineBase
	{
		public RootTimeLines () : base (null)
		{
			TimeLines = new ObservableCollection<TimelineBase> ();
		}
	}

	public class TimelineInfo : TimelineBase, INotifyPropertyChanged
	{
		TimelineInfo (TimelineBase owner, TwitterTimeLine timeline) : base (owner)
		{
			Statuses = timeline;
		}

		public TimelineInfo (TimelineBase owner, TwitterAccount account, TwitterTimeLine timeline) : this (owner, timeline)
		{
			RestAccount = account;
			Title = account.ScreenName + "'s " +
				(timeline == account.HomeTimeline ? "home" :
				(timeline == account.Mentions ? "mentions" : "dm"));
			RestUsage = (timeline == account.HomeTimeline ? account.RestHome :
				(timeline == account.Mentions ? account.RestMentions : account.RestDirectMessages));
		}

		public TimelineInfo (TimelineBase owner, SearchStatuses search) : this (owner, search.Statuses)
		{
			Search = search;
			RestAccount = search.Account;
			RestUsage = search.RestInfo;
			Title = "Search \"" + search.Keyword + "\"";
		}

		public string Title { get; set; }
		public TwitterAccount RestAccount { get; private set; }
		public TwitterAccount.RestUsage RestUsage { get; private set; }
		public TwitterTimeLine Statuses { get; private set; }
		public SearchStatuses Search { get; private set; }
		public StreamingClient StreamingClient {
			get {
				if (Search != null)
					return Search.StreamingClient;
				if (RestUsage == RestAccount.RestDirectMessages)
					return null;
				return RestAccount.StreamingClient;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		public void UpdateStreamingConstruction ()
		{
			if (PropertyChanged != null)
				PropertyChanged (this, new PropertyChangedEventArgs ("StreamingClient"));
		}
	}

	public class TabInfo : TimelineBase, INotifyPropertyChanged
	{
		public TabInfo (TimelineBase owner, string title) : base (owner)
		{
			Title = title;
			TimeLines = new ObservableCollection<TimelineBase> ();
		}

		public override void Insert (int idx, TimelineBase tl)
		{
			base.Insert (idx, tl);
			SelectedItem = tl;
		}

		public string Title { get; set; }

		TimelineBase _selectedItem = null;
		public TimelineBase SelectedItem {
			get { return _selectedItem; }
			set {
				_selectedItem = value;
				if (PropertyChanged != null)
					PropertyChanged (this, new PropertyChangedEventArgs ("SelectedItem"));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}

	public class HogeTemplateSelector : DataTemplateSelector
	{
		public DataTemplate TimelineTemplate { get; set; }
		public DataTemplate TabTemplate { get; set; }

		public override DataTemplate SelectTemplate (object item, DependencyObject container)
		{
			if (item is TimelineInfo)
				return TimelineTemplate;
			if (item is TabInfo)
				return TabTemplate;
			return base.SelectTemplate (item, container);
		}
	}

	public class TimeSpanToSecConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			TimeSpan span = (TimeSpan)value;
			if (span == null)
				return value;
			return (int)span.TotalSeconds;
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException ();
		}
	}
}
