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
		TwitterAccount[] _accounts;
		ObservableCollection<TwitterAccount> _observableAccountList;

		public PreferenceWindow (TwitterAccount[] accounts)
		{
			InitializeComponent ();
			_accounts = accounts;
			_observableAccountList = new ObservableCollection<TwitterAccount> (accounts);
			listAccounts.DataContext = _observableAccountList;
			this.Closed += delegate (object sender, EventArgs e) {
				_accounts = _observableAccountList.ToArray<TwitterAccount> ();
			};
		}

		public TwitterAccount[] Accounts {
			get { return _accounts; }
		}

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
			} catch {
				MessageBox.Show ("認証に失敗しました");
			}
		}

		private void DeleteButton_Click (object sender, RoutedEventArgs e)
		{
			TwitterAccount selected = (TwitterAccount)((Button)sender).DataContext;
			if (MessageBox.Show ("アカウントを削除してもよろしいですか？", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
				return;
			_observableAccountList.Remove (selected);
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
