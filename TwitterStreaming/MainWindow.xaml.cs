﻿/*
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
	public partial class MainWindow : Window
	{
		TwitterAccountManager _mgr;
		ObservableCollection<object> _timelines = new ObservableCollection<object> ();
		delegate void EmptyDelegate ();

		Status _replyInfo;
		string _replyName;

		public MainWindow ()
		{
			InitializeComponent ();
			itemsControl.DataContext = this;
			_mgr = new TwitterAccountManager ();
			_mgr.Load ();

			postAccount.ItemsSource = _mgr.Accounts;
			if (_mgr.Accounts.Length > 0)
				postAccount.SelectedIndex = 0;
		}

		private void MenuItem_AddNewTimeline_Click (object sender, RoutedEventArgs e)
		{
			NewTimelineWindow win = new NewTimelineWindow (_mgr);
			win.Owner = this;
			bool? ret = win.ShowDialog ();
			if (!ret.HasValue || !ret.Value)
				return;

			object info = null;
			TwitterAccount account = win.SelectedAccount;
			if (win.IsCheckedAccountTimeline) {
				info = new TimelineInfo (account, win.SelectedAccountTimeline);
			} else if (win.IsCheckedNewSearch && win.SearchKeyword.Length > 0) {
				SearchStatuses search = new SearchStatuses (account, win.SearchKeyword);
				if (win.IsUseStreamingForSearch)
					search.StreamingClient = new StreamingClient (new TwitterAccount[] {account}, search.Keyword, search);
				_mgr.AddSearchInfo (search);
				info = new TimelineInfo (search);
			} else if (win.IsCheckedExistedSearch && win.SelectedExistedSearch != null) {
				info = new TimelineInfo (win.SelectedExistedSearch);
			} else if (win.IsCheckedNewTab && win.NewTabTitle.Length > 0) {
				info = new TabInfo (win.NewTabTitle);
			}
			if (info != null)
				_timelines.Add (info);
		}

		private void MenuItem_ShowPreference_Click (object sender, RoutedEventArgs e)
		{
			PreferenceWindow win = new PreferenceWindow (_mgr);
			win.Owner = this;
			win.ShowDialog ();
			if (win.IsAccountArrayChanged) {
				_mgr.UpdateAccounts (win.Accounts);
				postAccount.ItemsSource = _mgr.Accounts;
			}

			if (win.IsStreamingTargetsChanged) {
				_mgr.ReconstructAllStreaming (win.StreamingTargets);
				for (int i = 0; i < _timelines.Count; i ++)
					if (_timelines[i] is TimelineInfo)
						(_timelines[i] as TimelineInfo).UpdateStreamingConstruction ();
			}

			_mgr.Save ();
		}

		public ObservableCollection<object> TimeLines {
			get { return _timelines; }
		}

		bool UseTimeline (TwitterTimeLine tl)
		{
			for (int i = 0; i < _timelines.Count; i ++) {
				TimelineInfo info = _timelines[i] as TimelineInfo;
				if (info != null) {
					if (info.Statuses == tl)
						return true;
				}
			}
			return false;
		}

		private void TimeLineCloseButton_Click (object sender, RoutedEventArgs e)
		{
			TimelineInfo info = (sender as Button).DataContext as TimelineInfo;
			if (MessageBox.Show (info.Title + " を閉じてもよろしいですか?", string.Empty, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
				_timelines.Remove (info);
				if (!UseTimeline (info.Statuses)) {
					_mgr.CloseTimeLine (info.Statuses);
					_mgr.Save ();
				}
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
			Status retweeted = account.TwitterClient.Retweet (status.ID);
			account.HomeTimeline.Add (retweeted);
		}
	}

	public class TimelineInfo : INotifyPropertyChanged
	{
		TimelineInfo (TwitterTimeLine timeline)
		{
			Statuses = timeline;
		}

		public TimelineInfo (TwitterAccount account, TwitterTimeLine timeline) : this (timeline)
		{
			RestAccount = account;
			Title = account.ScreenName + "'s " +
				(timeline == account.HomeTimeline ? "home" :
				(timeline == account.Mentions ? "mentions" : "dm"));
		}

		public TimelineInfo (SearchStatuses search) : this (search.Statuses)
		{
			Search = search;
			RestAccount = search.Account;
			Title = "Search \"" + search.Keyword + "\"";
		}

		public string Title { get; set; }
		public TwitterAccount RestAccount { get; private set; }
		public TwitterTimeLine Statuses { get; private set; }
		public SearchStatuses Search { get; private set; }
		public StreamingClient StreamingClient {
			get {
				if (Search != null)
					return Search.StreamingClient;
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
