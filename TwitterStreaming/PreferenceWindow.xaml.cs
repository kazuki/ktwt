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
using System.Windows.Media;
using ktwt.OAuth;

namespace TwitterStreaming
{
	public partial class PreferenceWindow : Window
	{
		TwitterAccount[] _accounts, _oldAccounts;
		ObservableCollection<TwitterAccount> _observableAccountList;
		object[] _targetList;
		IStreamingHandler[] _targets;
		MainWindow _mwin;
		TwitterAccountManager _mgr;

		public PreferenceWindow (TwitterAccountManager mgr, MainWindow mwin)
		{
			_mwin = mwin;
			_mgr = mgr;
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
			List<object> list = new List<object> ();
			list.Add ("null");
			for (int i = 0; i < mgr.Accounts.Length; i ++) list.Add (mgr.Accounts[i]);
			for (int i = 0; i < mgr.Searches.Length; i ++) list.Add (mgr.Searches[i]);
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
						if (_accounts[i].StreamingClient != null && _accounts[i].StreamingClient.Target == _targets[i])
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

		public MainWindow MainWindow {
			get { return _mwin; }
		}

		public IEnumerable<FontFamily> FontFamilies {
			get { return Fonts.SystemFontFamilies; }
		}

		public TwitterAccountManager AccountManager {
			get { return _mgr; }
		}

		private static double[] StandardFontSizes = new double[] {
            3.0d,    4.0d,   5.0d,   6.0d,   6.5d,   7.0d,   7.5d,   8.0d,   8.5d,   9.0d,
            9.5d,   10.0d,  10.5d,  11.0d,  11.5d,  12.0d,  12.5d,  13.0d,  13.5d,  14.0d,
            15.0d,  16.0d,  17.0d,  18.0d,  19.0d,
            20.0d,  22.0d,  24.0d,  26.0d,  28.0d,  30.0d,  32.0d,  34.0d,  36.0d,  38.0d,
            40.0d,  44.0d,  48.0d,  52.0d,  56.0d,  60.0d,  64.0d,  68.0d,  72.0d,  76.0d,
            80.0d,  88.0d,  96.0d, 104.0d, 112.0d, 120.0d, 128.0d, 136.0d, 144.0d, 152.0d,
           160.0d, 176.0d, 192.0d, 208.0d, 224.0d, 240.0d, 256.0d, 272.0d, 288.0d, 304.0d,
           320.0d, 352.0d, 384.0d, 416.0d, 448.0d, 480.0d, 512.0d, 544.0d, 576.0d, 608.0d,
           640.0d
		};
		public double[] FontSizes {
			get { return StandardFontSizes; }
		}

		public bool IsAccountArrayChanged { get; private set; }
		public bool IsStreamingTargetsChanged { get; private set; }

		private void Button_Click (object sender, RoutedEventArgs e)
		{
			LoginWindow win = new LoginWindow ();
			win.Owner = this;
			bool? ret = win.ShowDialog ();
			if (!ret.HasValue || !ret.Value)
				return;
			TwitterAccount account = new TwitterAccount (_mgr);
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

		private void HashTag_AddButton_Click (object sender, RoutedEventArgs e)
		{
			HashTagInputWindow win = new HashTagInputWindow ();
			win.Owner = this;
			bool? ret = win.ShowDialog ();
			if (!ret.HasValue || !ret.Value)
				return;
			string hashTag = win.HashTagText;
			for (int i = 0; i < _mwin.HashTagList.Count; i ++)
				if (hashTag.Equals (_mwin.HashTagList[i], StringComparison.InvariantCultureIgnoreCase)) {
					MessageBox.Show ("すでに同じハッシュタグが登録されています");
					return;
				}
			_mwin.HashTagList.Add (hashTag);
		}

		private void HashTag_DelButton_Click (object sender, RoutedEventArgs e)
		{
			if (hashTagList.SelectedIndex <= 0) return;
			_mwin.HashTagList.RemoveAt (hashTagList.SelectedIndex);
		}
	}

	[ValueConversion (typeof (SolidColorBrush), typeof (string))]
	public class ColorCodeNameConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			SolidColorBrush b = value as SolidColorBrush;
			if (b == null) return string.Empty;
			return "#" + b.Color.R.ToString ("X2") + b.Color.G.ToString ("X2") + b.Color.B.ToString ("X2");
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			string code = value as string;
			if (code == null || (code.Length != 4 && code.Length != 7) || code[0] != '#') return code;
			int lpc = (code.Length == 4 ? 1 : 2);
			byte[] rgb = new byte[3];
			try {
				for (int i = 0; i < 3; i ++) {
					rgb[i] = byte.Parse (code.Substring (1 + lpc * i, lpc), System.Globalization.NumberStyles.HexNumber);
					if (lpc == 1) rgb[i] = (byte)(rgb[i] * 16 + rgb[i]);
				}
				return new SolidColorBrush (Color.FromRgb (rgb[0], rgb[1], rgb[2]));
			} catch {
				return code;
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
			try {
				return TimeSpan.FromSeconds (int.Parse ((string)value));
			} catch {
				return value;
			}
		}
	}

	class DoubleToIntegerValueConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return (int)(double)value;
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return (double)(int)value;
		}
	}

}
