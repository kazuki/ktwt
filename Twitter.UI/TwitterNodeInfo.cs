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
using ktwt.OAuth;
using ktwt.ui;

namespace ktwt.Twitter.ui
{
	class TwitterNodeInfo : IStatusSourceNodeInfo
	{
		private TwitterNodeInfo () {}

		static TwitterNodeInfo _instance = new TwitterNodeInfo ();
		public static TwitterNodeInfo Instance { get { return _instance; } }

		public string SourceType {
			get { return "Twitter"; }
		}

		public Type StatusType {
			get { return typeof (Status); }
		}

		public Type AccountInfoType {
			get { return typeof (TwitterAccountInfo); }
		}

		public IStatusRenderer Renderer {
			get { return TweetRenderer.Instance; }
		}

		public Dictionary<string, string> SerializeAccountInfo (IAccountInfo obj)
		{
			TwitterAccountInfo info = (TwitterAccountInfo)obj;
			Dictionary<string, string> dic = new Dictionary<string,string> ();
			dic.Add ("user_id", info.Credential.UserID.ToString ());
			dic.Add ("screen_name", info.Credential.ScreenName);
			dic.Add ("token", info.Credential.AccessToken);
			dic.Add ("secret", info.Credential.AccessSecret);
			return dic;
		}

		public IAccountInfo DeserializeAccountInfo (Dictionary<string, string> dic)
		{
			return new TwitterAccountInfo (new TwitterOAuthCredentialCache (
				ulong.Parse (dic["user_id"]), dic["screen_name"], dic["token"], dic["secret"]
			));
		}

		public IAccountInfo CreateAccountWithGUI (Window owner)
		{
			OAuthClient client = new OAuthClient (AppKeyStore.Key, AppKeyStore.Secret,
				TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL, TwitterClient.XAuthURL);

			Uri uri;
			try {
				uri = client.GetAuthorizeURL ();
			} catch {
				MessageBox.Show ("oAuth認証用のトークンをTwitterに要求中にエラーが発生しました．しばらく時間をおいてから再試行してください．", string.Empty, MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
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
						return null;
					}
				}
			}
			
			PinInputWindow win = new PinInputWindow ();
			win.Owner = owner;
			Dictionary<string, string> contents;
			WebHeaderCollection headers;
			while (true) {
				if (win.ShowDialog () != true)
					return null;
				try {
					client.InputPIN (win.PIN, out contents, out headers);
					break;
				} catch {
					MessageBox.Show ("oAuthの認証に失敗しました．PIN番号が正しく入力されているかチェックしてください．", string.Empty, MessageBoxButton.OKCancel, MessageBoxImage.Error);
				}
			}

			TwitterOAuthCredentialCache cache = new TwitterOAuthCredentialCache (ulong.Parse (contents["user_id"]), contents["screen_name"], (OAuthCredentialCache)client.Credentials);
			return new TwitterAccountInfo (cache);
		}
	}
}
