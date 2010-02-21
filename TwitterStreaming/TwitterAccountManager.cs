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
using System.IO;
using System.Net;
using System.Threading;
using ktwt.Json;
using ktwt.OAuth;

namespace TwitterStreaming
{
	public class TwitterAccountManager
	{
		object _lock = new object ();
		TwitterAccount[] _accounts;
		List<SearchStatuses> _searches = new List<SearchStatuses> ();
		Thread _restThread;

		public TwitterAccountManager ()
		{
			_accounts = new TwitterAccount[0];
			_restThread = new Thread (RestThread);
			_restThread.IsBackground = true;
			_restThread.Start ();
		}

		public void UpdateAccounts (TwitterAccount[] accounts)
		{
			lock (_lock) {
				_accounts = accounts;
			}
		}

		public TwitterAccount[] Accounts {
			get { return _accounts; }
		}

		public void AddSearchInfo (SearchStatuses search)
		{
			lock (_searches) {
				_searches.Add (search);
			}
		}

		public void CloseTimeLine (TwitterTimeLine timeline)
		{
			IDisposable disposeObj = null;
			lock (_searches) {
				for (int i = 0; i < _searches.Count; i ++)
					if (_searches[i].Statuses == timeline) {
						disposeObj = _searches[i];
						_searches.RemoveAt (i);
						break;
					}
			}
			if (disposeObj != null)
				disposeObj.Dispose ();
		}

		void RestThread ()
		{
			while (true) {
				List<IUpdateChecker> list = new List<IUpdateChecker> ();
				lock (_lock) {
					list.AddRange (_accounts);
				}
				lock (_searches) {
					for (int i = 0; i < _searches.Count; i ++)
						list.Add (_searches[i]);
				}
				for (int i = 0; i < list.Count; i++) {
					try {
						list[i].UpdateTimeLines ();
					} catch {}
				}
				Thread.Sleep (1000);
			}
		}

		#region Load / Save
		static string ConfigFilePath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "ktwt.config.json");

		public bool Load ()
		{
			if (!File.Exists (ConfigFilePath))
				return false;
			try {
				JsonObject root = (JsonObject)new JsonValueReader (new StreamReader (ConfigFilePath, System.Text.Encoding.UTF8)).Read ();
				if (root.Value.ContainsKey ("accounts")) {
					JsonArray array = (JsonArray)root.Value["accounts"];
					TwitterAccount[] accounts = new TwitterAccount[array.Length];
					for (int i = 0; i < array.Length; i++)
						accounts[i] = LoadAccount ((JsonObject)array[i]);
					UpdateAccounts (accounts);
				}
				return true;
			} catch {
				return false;
			}
		}

		public void Save ()
		{
			using (StreamWriter streamWriter = new StreamWriter (ConfigFilePath, false, System.Text.Encoding.UTF8))
			using (JsonTextWriter writer = new JsonTextWriter (streamWriter)) {
				writer.WriteStartObject ();
				{
					writer.WriteKey ("accounts");
					writer.WriteStartArray ();
					for (int i = 0; i < _accounts.Length; i ++)
						WriteAccount (writer, _accounts[i]);
					writer.WriteEndArray ();
				}
				writer.WriteEndObject ();
			}
		}

		TwitterAccount LoadAccount (JsonObject obj)
		{
			string uname = (obj.Value["username"] as JsonString).Value;
			string password = (obj.Value["password"] as JsonString).Value;
			ICredentials credential = null;
			if (obj.Value.ContainsKey ("token")) {
				string token = (obj.Value["token"] as JsonString).Value;
				string secret = (obj.Value["secret"] as JsonString).Value;
				credential = new OAuthPasswordCache (uname, password, token, secret);
			} else {
				credential = new NetworkCredential (uname, password);
			}

			TwitterAccount account = new TwitterAccount ();
			account.Credential = credential;

			JsonObject streaming = obj.Value["streaming"] as JsonObject;
			if (streaming != null) {
				switch ((streaming.Value["mode"] as JsonString).Value) {
					case "follow":
						account.UpdateOAuthAccessToken ();
						account.TwitterClient.UpdateFriends ();
						account.StreamingClient = new StreamingClient (account, account.TwitterClient.Friends);
						break;
					/*case "track":
						account.StreamingClient = new StreamingClient (account, (streaming.Value["keywords"] as JsonString).Value);
						break;*/
				}
			}
			return account;
		}

		void WriteAccount (JsonTextWriter writer, TwitterAccount account)
		{
			writer.WriteStartObject ();
			if (account.Credential is NetworkCredential) {
				NetworkCredential nc = account.Credential as NetworkCredential;
				writer.WriteKey ("username");
				writer.WriteString (nc.UserName);
				writer.WriteKey ("password");
				writer.WriteString (nc.Password);
			} else if (account.Credential is OAuthPasswordCache) {
				OAuthPasswordCache pc = account.Credential as OAuthPasswordCache;
				writer.WriteKey ("username");
				writer.WriteString (pc.UserName);
				writer.WriteKey ("password");
				writer.WriteString (pc.Password);
				writer.WriteKey ("token");
				writer.WriteString (pc.AccessToken);
				writer.WriteKey ("secret");
				writer.WriteString (pc.AccessSecret);
			}
			writer.WriteKey ("streaming");
			if (account.StreamingClient == null) {
				writer.WriteNull ();
			} else {
				writer.WriteStartObject ();
				writer.WriteKey ("mode");
				if (account.StreamingClient.IsFollowMode) {
					writer.WriteString ("follow");
				} /*else if (account.StreamingClient.IsTrackMode) {
					writer.WriteString ("track");
					writer.WriteKey ("keywords");
					writer.WriteString (account.StreamingClient.SearchKeywords);
				}*/
				writer.WriteEndObject ();
			}
			writer.WriteEndObject ();
		}
		#endregion
	}
}
