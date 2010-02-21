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
		TwitterAccount[] _accounts;
		SearchStatuses[] _searches = new SearchStatuses[0];
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
			HashSet<TwitterAccount> oldSet = new HashSet<TwitterAccount> (_accounts);
			_accounts = accounts;
			oldSet.ExceptWith (accounts);
			foreach (TwitterAccount account in oldSet) {
				if (account.StreamingClient != null)
					account.StreamingClient.Dispose ();
			}
		}

		public TwitterAccount[] Accounts {
			get { return _accounts; }
		}

		public void AddSearchInfo (SearchStatuses search)
		{
			List<SearchStatuses> list = new List<SearchStatuses> (_searches);
			list.Add (search);
			_searches = list.ToArray ();
		}

		public SearchStatuses[] Searches {
			get { return _searches; }
		}

		public void CloseTimeLine (TwitterTimeLine timeline)
		{
			IDisposable disposeObj = null;
			List<SearchStatuses> list = new List<SearchStatuses> (_searches);
			for (int i = 0; i < list.Count; i++)
				if (list[i].Statuses == timeline) {
					disposeObj = list[i];
					list.RemoveAt (i);
					_searches = list.ToArray ();
					break;
				}
			if (disposeObj != null)
				disposeObj.Dispose ();
		}

		void RestThread ()
		{
			while (true) {
				List<IUpdateChecker> list = new List<IUpdateChecker> (_accounts);
				list.AddRange (_searches);
				for (int i = 0; i < list.Count; i++) {
					try {
						list[i].UpdateTimeLines ();
					} catch {}
				}
				Thread.Sleep (1000);
			}
		}

		#region Streaming Helpers
		public void ReconstructAllStreaming (IStreamingHandler[] targets)
		{
			if (Accounts.Length != targets.Length)
				throw new ArgumentException ();

			CloseAllStreaming ();

			Dictionary<IStreamingHandler, List<TwitterAccount>> dic = new Dictionary<IStreamingHandler, List<TwitterAccount>> ();
			for (int i = 0; i < Accounts.Length; i++) {
				IStreamingHandler h = targets[i];
				if (h == null)
					continue;
				List<TwitterAccount> list;
				if (!dic.TryGetValue (h, out list)) {
					list = new List<TwitterAccount> ();
					dic.Add (h, list);
				}
				list.Add (Accounts[i]);
			}
			foreach (KeyValuePair<IStreamingHandler, List<TwitterAccount>> pair in dic) {
				TwitterAccount homeTarget = pair.Key as TwitterAccount;
				SearchStatuses searchTarget = pair.Key as SearchStatuses;
				if (homeTarget != null)
					new StreamingClient (pair.Value.ToArray (), homeTarget.TwitterClient.Friends, homeTarget);
				else if (searchTarget != null)
					searchTarget.StreamingClient = new StreamingClient (pair.Value.ToArray (), searchTarget.Keyword, searchTarget);
			}
		}
		public void CloseAllStreaming ()
		{
			for (int i = 0; i < Accounts.Length; i++) {
				if (Accounts[i].StreamingClient != null)
					Accounts[i].StreamingClient.Dispose ();
			}
		}
		#endregion

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
					JsonArray accountsArray = array;
					TwitterAccount[] accounts = new TwitterAccount[array.Length];
					for (int i = 0; i < array.Length; i++)
						accounts[i] = LoadAccount ((JsonObject)array[i]);
					UpdateAccounts (accounts);

					array = (JsonArray)root.Value["searches"];
					SearchStatuses[] searches = new SearchStatuses[array.Length];
					for (int i = 0; i < array.Length; i ++)
						searches[i] = LoadSearch ((JsonObject)array[i], accounts);
					_searches = searches;

					IStreamingHandler[] targets = new IStreamingHandler[accountsArray.Length];
					for (int i = 0; i < accountsArray.Length; i ++)
						targets[i] = LoadStreamingTarget ((JsonObject)accountsArray[i], accounts, _searches);
					ReconstructAllStreaming (targets);
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
				
				writer.WriteKey ("accounts");
				writer.WriteStartArray ();
				for (int i = 0; i < _accounts.Length; i ++)
					WriteAccount (writer, _accounts[i]);
				writer.WriteEndArray ();

				writer.WriteKey ("searches");
				writer.WriteStartArray ();
				for (int i = 0; i < _searches.Length; i ++)
					WriteSearch (writer, _searches[i]);
				writer.WriteEndArray ();
				
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
				if (account.StreamingClient.Target is TwitterAccount) {
					writer.WriteString ("follow");
					writer.WriteKey ("username");
					writer.WriteString ((account.StreamingClient.Target as TwitterAccount).ScreenName);
				} else if (account.StreamingClient.Target is SearchStatuses) {
					writer.WriteString ("track");
					writer.WriteKey ("keywords");
					writer.WriteString (account.StreamingClient.SearchKeywords);
				}
				writer.WriteEndObject ();
			}
			writer.WriteEndObject ();
		}

		IStreamingHandler LoadStreamingTarget (JsonObject obj, TwitterAccount[] accounts, SearchStatuses[] searches)
		{
			JsonObject root = obj.Value["streaming"] as JsonObject;
			if (root == null) return null;
			string mode = (root.Value["mode"] as JsonString).Value;
			switch (mode) {
				case "follow":
					string username = (root.Value["username"] as JsonString).Value;
					for (int i = 0; i < accounts.Length; i ++)
						if (username.Equals (accounts[i].ScreenName))
							return accounts[i];
					break;
				case "track":
					string keywords = (root.Value["keywords"] as JsonString).Value;
					for (int i = 0; i < searches.Length; i ++)
						if (keywords.Equals (searches[i].Keyword))
							return searches[i];
					break;
			}
			return null;
		}

		SearchStatuses LoadSearch (JsonObject obj, TwitterAccount[] accounts)
		{
			string keywords = (obj.Value["keywords"] as JsonString).Value;
			string username = (obj.Value["username"] as JsonString).Value;
			for (int i = 0; i < accounts.Length; i ++) {
				if (accounts[i].ScreenName == username)
					return new SearchStatuses (accounts[i], keywords);
			}
			throw new Exception ();
		}

		void WriteSearch (JsonTextWriter writer, SearchStatuses search)
		{
			writer.WriteStartObject ();
			writer.WriteKey ("keywords");
			writer.WriteString (search.Keyword);
			writer.WriteKey ("username");
			writer.WriteString (search.Account.ScreenName);
			writer.WriteEndObject ();
		}
		#endregion
	}
}
