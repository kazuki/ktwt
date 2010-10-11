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
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using ktwt.OAuth;
using ktwt.Twitter;

namespace ktwt.ui
{
	public partial class OptionWindow : Window
	{
		public OptionWindow (Configurations config)
		{
			Config = config.Clone ();
			InitializeComponent ();
			btnOK.SizeChanged += delegate (object sender, SizeChangedEventArgs e) {
				OverlapMargin = new Thickness (5, 5, 5, 10.0 + btnOK.ActualHeight);
			};
		}

		public Configurations Config { get; set; }

		#region Account

		#region Twitter

		private void  TwitterAccount_Add_Click (object sender, RoutedEventArgs e)
		{
			OAuthClient client = new OAuthClient (AppKeyStore.Key, AppKeyStore.Secret,
				TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL, TwitterClient.XAuthURL);

			Uri uri;
			try {
				uri = client.GetAuthorizeURL ();
			} catch {
				MessageBox.Show ("oAuth認証用のトークンをTwitterに要求中にエラーが発生しました．しばらく時間をおいてから再試行してください．", string.Empty, MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			try {
				Process.Start (uri.ToString ());
			} catch {
				MessageBoxResult mbr = MessageBox.Show ("URLをブラウザで開けませんでした．関連付けがなされていない可能性があります．" + Environment.NewLine +
												 "\"はい\"を押すと認証用URLをクリップボードにコピーしますので，ブラウザにペーストして認証用URLにアクセスしてください", string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Error);
				if (mbr == MessageBoxResult.Yes) {
					try {
						Clipboard.SetText (uri.ToString ());
					} catch {
						MessageBox.Show ("クリップボードに保存できませんでした．再試行してください．", string.Empty, MessageBoxButton.OK, MessageBoxImage.Error);
						return;
					}
				}
			}
			
			PinInputWindow win = new PinInputWindow ();
			win.Owner = this;
			Dictionary<string, string> contents;
			WebHeaderCollection headers;
			while (true) {
				if (win.ShowDialog () != true)
					return;
				try {
					client.InputPIN (win.PIN, out contents, out headers);
					break;
				} catch {
					MessageBox.Show ("oAuthの認証に失敗しました．PIN番号が正しく入力されているかチェックしてください．", string.Empty, MessageBoxButton.OKCancel, MessageBoxImage.Error);
				}
			}

			TwitterOAuthCredentialCache cache = new TwitterOAuthCredentialCache (ulong.Parse (contents["user_id"]), contents["screen_name"], (OAuthCredentialCache)client.Credentials);
			if (Config.TwitterAccounts != null) {
				for (int i = 0; i < Config.TwitterAccounts.Length; i ++) {
					if (Config.TwitterAccounts[i].UserID == cache.UserID) {
						MessageBox.Show (cache.ScreenName + " は既に登録されているアカウントです．", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
						return;
					}
				}
			}
			List<TwitterOAuthCredentialCache> list = new List<TwitterOAuthCredentialCache> ();
			if (Config.TwitterAccounts != null)
				list.AddRange (Config.TwitterAccounts);
			list.Add (cache);
			Config.TwitterAccounts = list.ToArray ();
			UpdateBindingTarget (this, "Config.TwitterAccounts");
		}

		private void  TwitterAccount_Remove_Click (object sender, RoutedEventArgs e)
		{
			MessageBox.Show ("Not Implemented");
		}

		private void  TwitterAccount_ReAuth_Click (object sender, RoutedEventArgs e)
		{
			MessageBox.Show ("Not Implemented");
		}
		#endregion

		#endregion

		#region Misc
		private void UpdateBindingTarget (object source, string path)
		{
			for (int i = 0; i < BindingGroup.BindingExpressions.Count; i ++) {
				BindingExpression exp = BindingGroup.BindingExpressions[i] as BindingExpression;
				if (exp == null) continue;
				if (exp.DataItem != source || exp.ParentBinding == null || exp.ParentBinding.Path == null) continue;
				if (path.Equals (exp.ParentBinding.Path.Path))
					exp.UpdateTarget ();
			}
		}
		#endregion

		#region Dependency Properties
		public static readonly DependencyProperty OverlapMarginProperty =
			DependencyProperty.Register ("OverlapMargin", typeof (Thickness), typeof (OptionWindow), new FrameworkPropertyMetadata (new Thickness (5), FrameworkPropertyMetadataOptions.AffectsMeasure, null));
		public Thickness OverlapMargin {
			get { return (Thickness)GetValue (OverlapMarginProperty); }
			set { SetValue (OverlapMarginProperty, value); }
		}
		#endregion

		private void OK_Click (object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void Cancel_Click (object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}
	}
}
