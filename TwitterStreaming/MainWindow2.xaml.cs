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
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class MainWindow2 : Window
	{
		TwitterAccountManager _mgr;
		ObservableCollection<object> _timelines = new ObservableCollection<object> ();
		delegate void EmptyDelegate ();

		Status _replyInfo;
		string _replyName;

		public MainWindow2 ()
		{
			InitializeComponent ();
			itemsControl.DataContext = this;
			_mgr = new TwitterAccountManager ();

			List<TwitterAccount> list = new List<TwitterAccount> ();
			HashSet<string> userNames = new HashSet<string> ();
			string lastUserName = string.Empty, lastPassword = string.Empty;
			while (true) {
				LoginWindow win = new LoginWindow ();
				win.chkTrack.Visibility = Visibility.Collapsed;
				win.txtTrackWords.Visibility = Visibility.Collapsed;
				win.txtUsername.Text = lastUserName;
				win.txtPassword.Password = lastPassword;
				bool? ret = win.ShowDialog ();
				if (!ret.HasValue || !ret.Value)
					break;
				TwitterAccount account = new TwitterAccount ();
				lastUserName = win.txtUsername.Text;
				lastPassword = win.txtPassword.Password;
				if (userNames.Contains (lastUserName)) {
					MessageBox.Show ("入力されたユーザ名はすでに認証済みアカウントとして登録されています。別なアカウント情報を指定してください。");
					continue;
				}
				account.Credential = new NetworkCredential (lastUserName, lastPassword);
				try {
					account.UpdateOAuthAccessToken ();
					account.TwitterClient.UpdateFriends ();
					list.Add (account);
					if (win.useFollowStreaming.IsChecked.HasValue && win.useFollowStreaming.IsChecked.Value)
						account.StreamingClient = new StreamingClient (account, account.TwitterClient.Friends);
					userNames.Add (lastUserName);
					lastUserName = null;
					lastPassword = null;
					if (MessageBox.Show ("認証に成功しました。別なアカウントの情報を続けて入力しますか？", string.Empty, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
						break;
				} catch {
					MessageBox.Show ("認証に失敗しました。再度入力してください");
				}
			}
			if (list.Count == 0) {
				MessageBox.Show ("認証に成功したアカウントの情報がないためアプリケーションを終了します");
				Application.Current.Shutdown ();
				return;
			}
			_mgr.UpdateAccounts (list.ToArray ());
			postAccount.ItemsSource = _mgr.Accounts;
			postAccount.SelectedIndex = 0;
		}

		private void MenuItem_AddNewTimeline_Click (object sender, RoutedEventArgs e)
		{
			NewTimelineWindow win = new NewTimelineWindow (_mgr);
			bool? ret = win.ShowDialog ();
			if (!ret.HasValue || !ret.Value)
				return;

			object info = null;
			TwitterAccount account = win.SelectedAccount;
			if (win.IsCheckedAccountTimeline) {
				string username = (string)account.Credential.GetType ().GetProperty ("UserName").GetValue (account.Credential, null);
				TwitterTimeLine tl = win.SelectedAccountTimeline;
				info = new TimelineInfo (username + "'s " + (tl == account.HomeTimeline ? "home" : tl == account.Mentions ? "mentions" : "dm"), tl);
			} else if (win.IsCheckedNewSearch && win.SearchKeyword.Length > 0) {
				SearchStatuses search = new SearchStatuses (account, win.SearchKeyword, win.IsUseStreamingForSearch);
				_mgr.AddSearchInfo (search);
				info = new TimelineInfo ("Search \"" + search.Keyword + "\"", search.Statuses);
			} else if (win.IsCheckedNewTab && win.NewTabTitle.Length > 0) {
				info = new TabInfo (win.NewTabTitle);
			}
			if (info != null)
				_timelines.Add (info);
		}

		public ObservableCollection<object> TimeLines {
			get { return _timelines; }
		}

		private void TimeLineCloseButton_Click (object sender, RoutedEventArgs e)
		{
			TimelineInfo info = (sender as Button).DataContext as TimelineInfo;
			if (MessageBox.Show (info.Title + " を閉じてもよろしいですか?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
				_timelines.Remove (info);
				_mgr.CloseTimeLine (info.Statuses);
			}
		}

		private void TimeLineMoveLeftButton_Click (object sender, RoutedEventArgs e)
		{
			if (_timelines.Count <= 1)
				return;
			int idx = _timelines.IndexOf ((sender as Button).DataContext);
			if (idx > 0)
				_timelines.Move (idx, idx - 1);
		}

		private void TimeLineMoveRightButton_Click (object sender, RoutedEventArgs e)
		{
			if (_timelines.Count <= 1)
				return;
			int idx = _timelines.IndexOf ((sender as Button).DataContext);
			if (idx >= 0 && idx < _timelines.Count - 1)
				_timelines.Move (idx, idx + 1);
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
			if (_replyInfo != null) {
				if (postTextBox.Text.Contains (_replyName))
					SetReplySetting ();
				else
					ResetReplySetting (true);
			}
		}

		private void postButton_Click (object sender, RoutedEventArgs e)
		{
			string txt = postTextBox.Text.Trim ();
			if (txt.Length == 0) return;
			postTextBox.IsReadOnly = true;
			postTextBox.Foreground = Brushes.DimGray;
			postButton.IsEnabled = false;
			if (_replyInfo != null && !txt.StartsWith (_replyName))
				ResetReplySetting (false);
			ThreadPool.QueueUserWorkItem (PostProcess, new object[] {
				txt,
				postAccount.SelectedItem
			});
		}

		void PostProcess (object o)
		{
			object[] items = (object[])o;
			string txt = (string)items[0];
			TwitterAccount account = (TwitterAccount)items[1];
			Status status = null;
			try {
				status = account.TwitterClient.Update (txt, (_replyInfo == null ? (ulong?)null : _replyInfo.ID), null, null);
			} catch {}
			Dispatcher.Invoke (new EmptyDelegate (delegate () {
				postTextBox.IsReadOnly = false;
				postTextBox.Foreground = Brushes.White;
				postButton.IsEnabled = true;
				if (status != null) {
					ResetReplySetting (false);
					postTextBox.Text = "";
					postTextBox.Focus ();
					account.HomeTimeline.Add (status);
				}
			}));
		}

		void SetReplySetting ()
		{
			postButton.Content = "Reply";
		}

		void ResetReplySetting (bool btnTextOnly)
		{
			if (!btnTextOnly) {
				_replyInfo = null;
				_replyName = null;
			}
			postButton.Content = "Post";
		}
	}

	public class TimelineInfo
	{
		public TimelineInfo (string title, TwitterTimeLine timeline)
		{
			Title = title;
			Statuses = timeline;
		}

		public string Title { get; set; }
		public TwitterTimeLine Statuses { get; private set; }
	}

	public class TabInfo
	{
		ObservableCollection<object> _timelines = new ObservableCollection<object> ();

		public TabInfo (string title)
		{
			Title = title;
		}

		public ObservableCollection<object> TimeLines {
			get { return _timelines; }
		}
		public string Title { get; set; }
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
}
