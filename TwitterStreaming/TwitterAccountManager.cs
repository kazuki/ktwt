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
using ktwt.Twitter;

namespace TwitterStreaming
{
	public class TwitterAccountManager
	{
		TwitterAccount[] _accounts;
		SearchStatuses[] _searches = new SearchStatuses[0];
		ListStatuses[] _lists = new ListStatuses[0];
		Thread _restThread;

		public event EventHandler AccountsPropertyChanged;

		public TwitterAccountManager ()
		{
			_accounts = new TwitterAccount[0];
			_restThread = new Thread (RestThread);
			_restThread.IsBackground = true;
			_restThread.Start ();
			IconCache.Init (this);

			// defaults
			HomeIncludeMentions = true;
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
			if (AccountsPropertyChanged != null)
				AccountsPropertyChanged (this, EventArgs.Empty);
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

		public void AddListInfo (ListStatuses ls)
		{
			List<ListStatuses> list = new List<ListStatuses> (_lists);
			list.Add (ls);
			_lists = list.ToArray ();
		}

		public ListStatuses[] Lists {
			get { return _lists; }
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

			List<ListStatuses> list2 = new List<ListStatuses> (_lists);
			for (int i = 0; i < list2.Count; i++)
				if (list2[i].Statuses == timeline) {
					disposeObj = list2[i];
					list2.RemoveAt (i);
					_lists = list2.ToArray ();
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
				list.AddRange (_lists);
				for (int i = 0; i < list.Count; i++) {
					try {
						list[i].UpdateTimeLines ();
					} catch {}
				}
				Thread.Sleep (1000);
			}
		}

		public bool HomeIncludeMentions { get; set; }

		#region Streaming Helpers
		public void ReconstructAllStreaming (IStreamingHandler[] targets, bool dummy)
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
				ListStatuses listTarget = pair.Key as ListStatuses;
				if (homeTarget != null) {
					ulong[] ids;
					try {
						ids = dummy ? null : homeTarget.TwitterClient.FriendIDs;
					} catch {
						continue;
					}
					new StreamingClient (pair.Value.ToArray (), ids, homeTarget, dummy);
				} else if (searchTarget != null) {
					searchTarget.StreamingClient = new StreamingClient (pair.Value.ToArray (), searchTarget.Keyword, searchTarget, dummy);
				} else if (listTarget != null) {
					listTarget.StreamingClient = new StreamingClient (pair.Value.ToArray (), listTarget.Account, listTarget.List, listTarget, dummy);
				}
			}
		}
		public void CloseAllStreaming ()
		{
			for (int i = 0; i < Accounts.Length; i++) {
				if (Accounts[i].StreamingClient != null)
					Accounts[i].StreamingClient.Dispose ();
			}
			for (int i = 0; i < _searches.Length; i ++)
				_searches[i].StreamingClient = null;
			for (int i = 0; i < _lists.Length; i++)
				_lists[i].StreamingClient = null;
		}
		#endregion

		#region Load / Save
		static string ConfigFilePath = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "ktwt.config.json");
		public delegate void ConfigLoadDelegate (JsonObject root);
		public delegate void ConfigSaveDelegate (JsonTextWriter writer);

		public bool Load (ConfigLoadDelegate load, out IStreamingHandler[] targets)
		{
			targets = null;
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

					if (root.Value.ContainsKey ("lists")) {
						array = (JsonArray)root.Value["lists"];
						List<ListStatuses> lists = new List<ListStatuses> ();
						for (int i = 0; i < array.Length; i++) {
							ListStatuses ls = LoadList ((JsonObject)array[i], accounts);
							if (ls != null)
								lists.Add (ls);
						}
						_lists = lists.ToArray ();
					}

					targets = new IStreamingHandler[accountsArray.Length];
					for (int i = 0; i < accountsArray.Length; i ++)
						targets[i] = LoadStreamingTarget ((JsonObject)accountsArray[i], accounts, _searches, _lists);
					ReconstructAllStreaming (targets, true);
				}
				load (root);
				return true;
			} catch {
				return false;
			}
		}

		public void Save (ConfigSaveDelegate save)
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

				writer.WriteKey ("lists");
				writer.WriteStartArray ();
				for (int i = 0; i < _lists.Length; i++)
					WriteList (writer, _lists[i]);
				writer.WriteEndArray ();

				save (writer);
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

			TwitterAccount account = new TwitterAccount (this);
			account.Credential = credential;
			if (obj.Value.ContainsKey ("id"))
				account.SelfUserID = (ulong)(obj.Value["id"] as JsonNumber).Value;
			if (account.SelfUserID == 0) {
				// Backward compatibility (~0.0.5)
				ThreadPool.QueueUserWorkItem (delegate (object o) {
					if (credential as OAuthCredentialCache == null)
						return;
					for (int i = 0; i < 5; i ++) {
						try {
							account.SelfUserID = account.TwitterClient.VerifyCredentials ().ID;
							break;
						} catch {}
						Thread.Sleep (5 * 1000);
					}
				});
			}
			if (obj.Value.ContainsKey ("rest")) {
				JsonObject restRoot = (obj.Value["rest"] as JsonObject);
				string[] rest_keys = new string[] {"home", "mentions", "dm"};
				TwitterAccount.RestUsage[] rests = new TwitterAccount.RestUsage[] {account.RestHome, account.RestMentions, account.RestDirectMessages};
				for (int i = 0; i < rest_keys.Length; i ++) {
					if (!restRoot.Value.ContainsKey (rest_keys[i])) continue;
					JsonObject restInfoRoot = (restRoot.Value[rest_keys[i]] as JsonObject);
					rests[i].IsEnabled = (restInfoRoot.Value["enable"] as JsonBoolean).Value;
					rests[i].Count = (int)(restInfoRoot.Value["count"] as JsonNumber).Value;
					rests[i].Interval = TimeSpan.FromSeconds ((restInfoRoot.Value["interval"] as JsonNumber).Value);
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
			if (account.SelfUserID > 0) {
				writer.WriteKey ("id");
				writer.WriteNumber (account.SelfUserID);
			}
			writer.WriteKey ("rest");
			writer.WriteStartObject ();
			string[] rest_keys = new string[] {"home", "mentions", "dm"};
			TwitterAccount.RestUsage[] rests = new TwitterAccount.RestUsage[] {account.RestHome, account.RestMentions, account.RestDirectMessages};
			for (int i = 0; i < rest_keys.Length; i ++) {
				writer.WriteKey (rest_keys[i]);
				writer.WriteStartObject ();
				writer.WriteKey ("enable");
				writer.WriteBoolean (rests[i].IsEnabled);
				writer.WriteKey ("count");
				writer.WriteNumber (rests[i].Count);
				writer.WriteKey ("interval");
				writer.WriteNumber ((int)rests[i].Interval.TotalSeconds);
				writer.WriteEndObject ();
			}
			writer.WriteEndObject ();
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
				} else if (account.StreamingClient.Target is ListStatuses) {
					writer.WriteString ("list");
					writer.WriteKey ("id");
					writer.WriteNumber ((account.StreamingClient.Target as ListStatuses).List.ID);
				}
				writer.WriteEndObject ();
			}
			writer.WriteEndObject ();
		}

		IStreamingHandler LoadStreamingTarget (JsonObject obj, TwitterAccount[] accounts, SearchStatuses[] searches, ListStatuses[] lists)
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
				case "list":
					ulong id = (ulong)(root.Value["id"] as JsonNumber).Value;
					for (int i = 0; i < lists.Length; i++)
						if (id == lists[i].List.ID)
							return lists[i];
					break;
			}
			return null;
		}

		SearchStatuses LoadSearch (JsonObject obj, TwitterAccount[] accounts)
		{
			string keywords = (obj.Value["keywords"] as JsonString).Value;
			string username = (obj.Value["username"] as JsonString).Value;
			for (int i = 0; i < accounts.Length; i ++) {
				if (accounts[i].ScreenName == username) {
					SearchStatuses ss = new SearchStatuses (accounts[i], keywords);
					LoadRestUsage (obj, ss.RestInfo);
					return ss;
				}
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
			WriteRestUsage (writer, search.RestInfo);
			writer.WriteEndObject ();
		}

		ListStatuses LoadList (JsonObject obj, TwitterAccount[] accounts)
		{
			ulong id = (ulong)(obj.Value["id"] as JsonNumber).Value;
			string username = (obj.Value["username"] as JsonString).Value;
			for (int i = 0; i < accounts.Length; i++) {
				if (accounts[i].ScreenName == username) {
					ListInfo[] lists = accounts[i].TwitterClient.SelfAndFollowingList;
					for (int j = 0; j < lists.Length; j ++) {
						if (lists[j].ID == id) {
							ListStatuses statuses = new ListStatuses (accounts[i], lists[j]);
							LoadRestUsage (obj, statuses.RestInfo);
							return statuses;
						}
					}
				}
			}
			return null;
		}

		void WriteList (JsonTextWriter writer, ListStatuses list)
		{
			writer.WriteStartObject ();
			writer.WriteKey ("id");
			writer.WriteNumber (list.List.ID);
			writer.WriteKey ("username");
			writer.WriteString (list.Account.ScreenName);
			WriteRestUsage (writer, list.RestInfo);
			writer.WriteEndObject ();
		}

		void LoadRestUsage (JsonObject obj, TwitterAccount.RestUsage usage)
		{
			if (obj.Value.ContainsKey ("interval"))
				usage.Interval = TimeSpan.FromSeconds ((obj.Value["interval"] as JsonNumber).Value);
			if (obj.Value.ContainsKey ("count"))
				usage.Count = (int)(obj.Value["count"] as JsonNumber).Value;
		}

		void WriteRestUsage (JsonTextWriter writer, TwitterAccount.RestUsage usage)
		{
			writer.WriteKey ("interval");
			writer.WriteNumber (usage.Interval.TotalSeconds);
			writer.WriteKey ("count");
			writer.WriteNumber (usage.Count);
		}
		#endregion
	}
}
