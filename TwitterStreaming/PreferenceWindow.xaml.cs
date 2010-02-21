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
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ktwt.OAuth;

namespace TwitterStreaming
{
	public partial class PreferenceWindow : Window
	{
		TwitterAccount[] _accounts, _oldAccounts;
		ObservableCollection<TwitterAccount> _observableAccountList;
		object[] _targetList;
		IStreamingHandler[] _targets;

		public PreferenceWindow (TwitterAccountManager mgr)
		{
			InitializeComponent ();
			_accounts = mgr.Accounts;
			_oldAccounts = (TwitterAccount[])_accounts.Clone ();
			_targets = new IStreamingHandler[_accounts.Length];
			for (int i = 0; i < _targets.Length; i ++)
				_targets[i] = _accounts[i].StreamingClient == null ? null : _accounts[i].StreamingClient.Target;

			StreamingTargetSelector selector = new StreamingTargetSelector ();
			selector.NullTemplate = (DataTemplate)Resources["nullTemplate"];
			selector.HomeTemplate = (DataTemplate)Resources["homeTemplate"];
			selector.SearchTemplate = (DataTemplate)Resources["searchTemplate"];
			Resources.Add ("targetTemplateSelector", selector);

			_observableAccountList = new ObservableCollection<TwitterAccount> (mgr.Accounts);
			SearchStatuses[] searches = mgr.GetSearches ();
			List<object> list = new List<object> ();
			list.Add ("null");
			for (int i = 0; i < mgr.Accounts.Length; i ++) list.Add (mgr.Accounts[i]);
			for (int i = 0; i < searches.Length; i ++) list.Add (searches[i]);
			_targetList = list.ToArray ();
			this.DataContext = _observableAccountList;
			this.Closed += delegate (object sender, EventArgs e) {
				_accounts = _observableAccountList.ToArray<TwitterAccount> ();
				IsAccountArrayChanged = false;

				do {
					if (_accounts.Length != _oldAccounts.Length) {
						IsAccountArrayChanged = true;
						break;
					}
					for (int i = 0; i < _accounts.Length; i++) {
						if (_accounts[i] != _oldAccounts[i]) {
							IsAccountArrayChanged = true;
							break;
						}
					}
				} while (false);

				IsStreamingTargetsChanged = IsAccountArrayChanged;
				do {
					for (int i = 0; i < _accounts.Length; i ++) {
						if (_accounts[i].StreamingClient == null && _targets[i] == null)
							continue;
						if (_accounts[i].StreamingClient.Target == _targets[i])
							continue;
						IsStreamingTargetsChanged = true;
						break;
					}
				} while (false);
			};
		}

		public TwitterAccount[] Accounts {
			get { return _accounts; }
		}

		public IStreamingHandler[] StreamingTargets {
			get { return _targets; }
		}

		public object[] StreamingTargetList {
			get { return _targetList; }
		}

		public bool IsAccountArrayChanged { get; private set; }
		public bool IsStreamingTargetsChanged { get; private set; }

		private void Button_Click (object sender, RoutedEventArgs e)
		{
			LoginWindow win = new LoginWindow ();
			bool? ret = win.ShowDialog ();
			if (!ret.HasValue || !ret.Value)
				return;
			TwitterAccount account = new TwitterAccount ();
			for (int i = 0; i < _observableAccountList.Count; i ++) {
				ICredentials c = _observableAccountList[i].Credential;
				string userName = (c is NetworkCredential ? (c as NetworkCredential).UserName : (c as OAuthPasswordCache).UserName);
				if (userName.Equals (win.UserName)) {
					MessageBox.Show ("入力されたユーザ名はすでにアカウントとして登録されています");
					return;
				}
			}
			account.Credential = new NetworkCredential (win.UserName, win.Password);
			try {
				account.UpdateOAuthAccessToken ();
				_observableAccountList.Add (account);
				Array.Resize<IStreamingHandler> (ref _targets, _observableAccountList.Count);
			} catch {
				MessageBox.Show ("認証に失敗しました");
			}
		}

		private void DeleteButton_Click (object sender, RoutedEventArgs e)
		{
			TwitterAccount selected = (TwitterAccount)((Button)sender).DataContext;
			if (MessageBox.Show ("アカウントを削除してもよろしいですか？", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;
			int idx = _observableAccountList.IndexOf (selected);
			if (idx < 0) return;
			_observableAccountList.Remove (selected);
			List<IStreamingHandler> list = new List<IStreamingHandler> (_targets);
			list.RemoveAt (idx);
			_targets = list.ToArray ();
		}

		class StreamingTargetSelector : DataTemplateSelector
		{
			public DataTemplate NullTemplate { get; set; }
			public DataTemplate HomeTemplate { get; set; }
			public DataTemplate SearchTemplate { get; set; }

			public override DataTemplate SelectTemplate (object item, DependencyObject container)
			{
				if (item is TwitterAccount)
					return HomeTemplate;
				if (item is SearchStatuses)
					return SearchTemplate;
				return NullTemplate;
			}
		}

		private void ComboBox_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			ComboBox cb = sender as ComboBox;
			IStreamingHandler selected = cb.SelectedItem as IStreamingHandler;
			if (cb.SelectedItem != null && selected == null)
				cb.SelectedItem = null;
			for (int i = 0; i < _accounts.Length; i ++) {
				if (_accounts[i] == cb.DataContext) {
					_targets[i] = selected;
					return;
				}
			}
		}
	}

	[ValueConversion (typeof (TimeSpan), typeof (string))]
	class TimeSpanToSecondsConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return ((int)((TimeSpan)value).TotalSeconds).ToString ();
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return TimeSpan.FromSeconds (int.Parse ((string)value));
		}
	}
}
