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
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace TwitterStreaming
{
	public partial class MainWindow2 : Window
	{
		TwitterAccountManager _mgr;
		ObservableCollection<object> _timelines = new ObservableCollection<object> ();

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
				_mgr.Searches.Add (search);
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
