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
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using ktwt.Json;
using ktwt.OAuth;

namespace ktwt.Twitter
{
	public class TwitterClient : INotifyPropertyChanged
	{
		public const int MaxStatusLength = 140;
		public const int OAuthApiLimitMax = 350;
		public const int BasicApiLimitMax = 150;

		public static Uri RequestTokenURL = new Uri ("https://twitter.com/oauth/request_token");
		public static Uri AccessTokenURL = new Uri ("https://twitter.com/oauth/access_token");
		public static Uri AuthorizeURL = new Uri ("https://twitter.com/oauth/authorize");

		const string StatusesHomeTimelineURL = "https://api.twitter.com/1/statuses/home_timeline.json";
		const string StatusesMentionsURL = "https://twitter.com/statuses/mentions.json";
		const string StatusesUpdateURL = "https://twitter.com/statuses/update.json";
		const string StatusesRetweetURL = "https://api.twitter.com/1/statuses/retweet/{0}.json";
		const string SearchURL = "http://search.twitter.com/search.json";
		static readonly Uri FriendIDsURL = new Uri ("https://twitter.com/friends/ids.json");
		const string UsersShowURL = "https://twitter.com/users/show.json";
		const string UserFriendsURL = "https://api.twitter.com/1/statuses/friends.json";
		const string UserFollowersURL = "https://api.twitter.com/1/statuses/followers.json";
		static readonly Uri AccountVerifyCredentialsURL = new Uri ("https://api.twitter.com/1/account/verify_credentials.json");
		const string FavoritesURL = "https://api.twitter.com/1/favorites.json";
		const string FavoritesUserURL = "https://api.twitter.com/1/favorites/{0}.json";
		const string FavoritesCreateURL = "https://api.twitter.com/1/favorites/create/{0}.json";
		const string FavoritesDestroyURL = "https://api.twitter.com/1/favorites/destroy/{0}.json";

		const string X_RateLimit_Limit = "X-RateLimit-Limit";
		const string X_RateLimit_Remaining = "X-RateLimit-Remaining";
		const string X_RateLimit_Reset = "X-RateLimit-Reset";
		static DateTime UnixTimeStart = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		const string HTTP_GET = "GET";
		const string HTTP_POST = "POST";
		const string UrlEncodedMime = "application/x-www-form-urlencoded";
		const string UserAgent = "ktwt";

		ISimpleWebClient _client;

		int _apiLimitMax = -1, _apiLimitRemaining = -1;
		DateTime _apiLimitResetTime = DateTime.MaxValue;

		User[] _friends = null, _followers = null;
		ulong[] _friendIDs = null;

		public event EventHandler ApiLimitChanged;

		public TwitterClient (ISimpleWebClient baseClient)
		{
			_client = baseClient;
		}

		#region API Info
		public int ApiLimitMax {
			get { return _apiLimitMax; }
			private set {
				_apiLimitMax = value;
				InvokePropertyChanged ("ApiLimitMax");
			}
		}

		public int ApiLimitRemaining {
			get { return _apiLimitRemaining; }
			private set {
				_apiLimitRemaining = value;
				InvokePropertyChanged ("ApiLimitRemaining");
			}
		}

		public DateTime ApiLimitResetTime {
			get { return _apiLimitResetTime; }
			private set {
				_apiLimitResetTime = value;
				InvokePropertyChanged ("ApiLimitResetTime");
			}
		}
		#endregion

		#region Timeline Methods
		public Status[] GetHomeTimeline (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetStatus (StatusesHomeTimelineURL, since_id, max_id, count, page);
		}

		public Status[] GetMentions (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetStatus (StatusesMentionsURL, since_id, max_id, count, page);
		}

		public Status[] GetStatus (string baseUrl, ulong? since_id, ulong? max_id, int? count, int? page)
		{
			string query = "";
			if (since_id.HasValue) query += "&since_id=" + since_id.Value.ToString ();
			if (max_id.HasValue) query += "&max_id=" + max_id.Value.ToString ();
			if (count.HasValue) query += "&count=" + count.Value.ToString ();
			if (page.HasValue) query += "&page=" + page.Value.ToString ();
			if (query.Length > 0) query = "?" + query.Substring (1);
			return GetStatus (new Uri (baseUrl + query));
		}

		Status[] GetStatus (Uri url)
		{
			string json = DownloadString (url, HTTP_GET, null);
			JsonArray array = (JsonArray)new JsonValueReader (json).Read ();
			Status[] statuses = new Status[array.Length];
			for (int i = 0; i < array.Length; i++)
				statuses[i] = JsonDeserializer.Deserialize<Status> ((JsonObject)array[i]);
			return statuses;
		}
		#endregion

		#region Status Methods
		public Status Update (string status, ulong? in_reply_to_status_id, string geo_lat, string geo_long)
		{
			if (status == null || status.Length == 0 || status.Length > MaxStatusLength)
				throw new ArgumentException ();
			string query = "?status=" + OAuthBase.UrlEncode (status);
			if (in_reply_to_status_id.HasValue) query += "&in_reply_to_status_id=" + in_reply_to_status_id.Value.ToString ();
			if (geo_lat != null && geo_lat.Length > 0) query += "&lat=" + geo_lat;
			if (geo_long != null && geo_long.Length > 0) query += "&long=" + geo_long;
			string json = DownloadString (new Uri (StatusesUpdateURL + query), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> ((JsonObject)new JsonValueReader (json).Read ());
		}

		public Status Retweet (ulong id)
		{
			string json = DownloadString (new Uri (string.Format (StatusesRetweetURL, id)), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> ((JsonObject)new JsonValueReader (json).Read ());
		}
		#endregion

		#region User Methods
		public User GetUserInfo (ulong? user_id, string screen_name)
		{
			if (user_id.HasValue && (screen_name != null && screen_name.Length > 0))
				throw new ArgumentException ();

			string query = null;
			if (user_id.HasValue) query = "?user_id=" + user_id.Value.ToString ();
			else if (screen_name != null && screen_name.Length > 0) query = "?screen_name=" + OAuthBase.UrlEncode (screen_name);
			else throw new ArgumentException ();

			string json = DownloadString (new Uri (UsersShowURL + query), HTTP_GET, null);
			return JsonDeserializer.Deserialize<User> ((JsonObject)new JsonValueReader (json).Read ());
		}

		public User[] GetFriends (ulong? user_id, string screen_name)
		{
			return GetFriendsOrFollowers (UserFriendsURL, user_id, screen_name);
		}

		public User[] GetFollowers (ulong? user_id, string screen_name)
		{
			return GetFriendsOrFollowers (UserFollowersURL, user_id, screen_name);
		}

		User[] GetFriendsOrFollowers (string url, ulong? user_id, string screen_name)
		{
			string query_base = string.Empty;
			if (user_id.HasValue)
				query_base = "?user_id=" + user_id.Value.ToString ();
			else if (screen_name != null && screen_name.Length > 0)
				query_base = "?screen_name=" + OAuthBase.UrlEncode (screen_name);

			query_base = (query_base.Length == 0 ? "?cursor=" : "&cursor=");
			string query = query_base + "-1";
			List<User> users = new List<User> ();
			while (true) {
				string json = DownloadString (new Uri (url + query), HTTP_GET, null);
				JsonObject obj = (JsonObject)new JsonValueReader (json).Read ();
				JsonArray array = (JsonArray)obj.Value["users"];
				for (int i = 0; i < array.Length; i++)
					users.Add (JsonDeserializer.Deserialize<User> ((JsonObject)array[i]));

				string next = (obj.Value["next_cursor_str"] as JsonString).Value;
				if (next == "0")
					break;
				query = query_base + next;
			}
			return users.ToArray ();
		}

		public void UpdateFriends ()
		{
			User[] friends = GetFriends (null, null);
			ulong[] ids = new ulong[friends.Length];
			for (int i = 0; i < ids.Length; i ++)
				ids[i] = friends[i].ID;
			_friends = friends;
			_friendIDs = ids;
		}

		public User[] Friends {
			get {
				if (_friends == null)
					UpdateFriends ();
				return _friends;
			}
		}

		public void UpdateFollowers ()
		{
			_followers = GetFollowers (null, null);
		}

		public User[] Followers {
			get {
				if (_followers == null)
					UpdateFollowers ();
				return _followers;
			}
		}
		#endregion

		#region Search API Methods
		public Status[] Search (string keywords, string lang, string locale, int? rpp, int? page, ulong? since_id, string geocode, bool? show_user)
		{
			if (keywords == null || keywords.Length == 0)
				throw new ArgumentException ();

			string query = "?q=" + OAuthBase.UrlEncode (keywords);
			if (lang != null && lang.Length > 0) query += "&lang=" + lang;
			if (locale != null && locale.Length > 0) query += "&locale=" + locale;
			if (rpp.HasValue) query += "&rpp=" + rpp.Value.ToString ();
			if (page.HasValue) query += "&page=" + page.Value.ToString ();
			if (since_id.HasValue) query += "&since_id=" + since_id.Value.ToString ();
			if (geocode != null && geocode.Length > 0) query += "&geocode=" + geocode;
			if (show_user.HasValue && show_user.Value) query += "&show_user=" + show_user.Value.ToString ();

			string json = DownloadStringWithoutAuthentication (new Uri (SearchURL + query), HTTP_GET, null);
			JsonObject rootObj = (JsonObject)new JsonValueReader (json).Read ();
			JsonArray array = (JsonArray)rootObj.Value["results"];
			Status[] statuses = new Status[array.Length];
			for (int i = 0; i < array.Length; i++) {
				JsonObject o = (JsonObject)array[i];
				statuses[i] = new Status {
					ID = (ulong)(o.Value["id"] as JsonNumber).Value,
					Text = (o.Value["text"] as JsonString).Value,
					CreatedAt = DateTime.ParseExact ((o.Value["created_at"] as JsonString).Value, "ddd, dd MMM yyyy HH:mm:ss zzzz", JsonDeserializer.InvariantCulture),
					User = new User {
						ScreenName = (o.Value["from_user"] as JsonString).Value,
						Name = (o.Value["from_user"] as JsonString).Value,
						ProfileImageUrl = (o.Value["profile_image_url"] as JsonString).Value
					}
				};
			}
			return statuses;
		}
		#endregion

		#region Social Graph Methods
		public void UpdateFriendIDs ()
		{
			string json = DownloadString (FriendIDsURL, HTTP_GET, null);
			JsonArray array = (JsonArray)new JsonValueReader (json).Read ();
			ulong[] friends = new ulong[array.Length];
			for (int i = 0; i < array.Length; i ++)
				friends[i] = (ulong)(array[i] as JsonNumber).Value;
			_friendIDs = friends;
		}

		public ulong[] FriendIDs {
			get {
				if (_friendIDs == null)
					UpdateFriendIDs ();
				return _friendIDs;
			}
		}
		#endregion

		#region Account Methods
		public User VerifyCredentials ()
		{
			string json = DownloadString (AccountVerifyCredentialsURL, HTTP_GET, null);
			return JsonDeserializer.Deserialize<User> ((JsonObject)new JsonValueReader (json).Read ());
		}
		#endregion

		#region Favorite Methods
		public Status[] GetFavorites (ulong? id, string screenName, int? page)
		{
			string url = FavoritesURL;
			if (id.HasValue) url = string.Format (FavoritesUserURL, id.Value);
			else if (screenName != null && screenName.Length > 0) url = string.Format (FavoritesUserURL, screenName);
			if (page.HasValue) url += "?page=" + page.Value.ToString ();

			string json = DownloadString (new Uri (url), HTTP_GET, null);
			JsonArray array = (JsonArray)new JsonValueReader (json).Read ();
			Status[] status = new Status[array.Length];
			for (int i = 0; i < array.Length; i ++)
				status[i] = JsonDeserializer.Deserialize<Status> ((JsonObject)array[i]);
			return status;
		}

		Status FavoritesExec (string url, ulong id)
		{
			string json = DownloadString (new Uri (string.Format (url, id)), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> ((JsonObject)new JsonValueReader (json).Read ());
		}

		public Status FavoritesCreate (ulong id)
		{
			return FavoritesExec (FavoritesCreateURL, id);
		}

		public Status FavoritesDestroy (ulong id)
		{
			return FavoritesExec (FavoritesDestroyURL, id);
		}
		#endregion

		#region Streaming API
		public IStreamingState StartStreaming (Uri uri, string method, string postData)
		{
			return StartStreaming (uri, method, postData, _client.Credentials);
		}

		public IStreamingState StartStreaming (Uri uri, string method, string postData, ICredentials credentials)
		{
			HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create (uri);
			req.Credentials = credentials;
			req.Method = method;
			if (method == HTTP_POST && postData != null && postData.Length > 0) {
				byte[] rawData = Encoding.ASCII.GetBytes (postData);
				req.ContentLength = rawData.Length;
				Stream strm = req.GetRequestStream ();
				strm.Write (rawData, 0, rawData.Length);
				req.ContentType = UrlEncodedMime;
			}

			HttpWebResponse res = (HttpWebResponse)req.GetResponse ();
			return new StreamingState (res);
		}

		public void StopStreaming (IStreamingState state)
		{
			StreamingState ss = state as StreamingState;
			if (ss == null)
				throw new ArgumentException ();
			ss.Dispose ();
		}

		class StreamingState : IStreamingState, IDisposable
		{
			HttpWebResponse _res;
			Stream _strm;
			bool _closed = false;

			public StreamingState (HttpWebResponse res)
			{
				_res = res;
				_strm = res.GetResponseStream ();
			}

			public HttpWebResponse Response {
				get { return _res; }
			}

			public Stream Stream {
				get { return _strm; }
			}

			public bool IsClosed {
				get { return _closed; }
			}

			public void Dispose ()
			{
				lock (this) {
					if (_closed)
						return;
					_closed = true;
				}
				try {
					_strm.Dispose ();
				} catch {}
				try {
					_res.Close ();
				} catch {}
			}
		}
		#endregion

		#region Misc
		public static ulong GetMaxStatusID (ulong? current, Status[] status)
		{
			if (!current.HasValue)
				current = 0;
			return GetMaxStatusID (current.Value, status);
		}

		public static ulong GetMaxStatusID (ulong current, Status[] status)
		{
			for (int i = 0; i < status.Length; i ++)
				current = Math.Max (current, status[i].ID);
			return current;
		}
		#endregion

		#region Internal Use
		string DownloadString (Uri uri, string method, byte[] postData)
		{
			WebHeaderCollection headers;
			return DownloadString (uri, method, postData, out headers);
		}

		string DownloadString (Uri uri, string method, byte[] postData, out WebHeaderCollection headers)
		{
			string text = _client.DownloadString (uri, method, postData, out headers);

			long temp;
			string value = headers[X_RateLimit_Limit];
			if (value != null && long.TryParse (value, out temp) && temp != BasicApiLimitMax) {
				ApiLimitMax = (int)temp;
				value = headers[X_RateLimit_Remaining];
				if (value != null && long.TryParse (value, out temp)) ApiLimitRemaining = (int)temp;
				value = headers[X_RateLimit_Reset];
				if (value != null && long.TryParse (value, out temp)) ApiLimitResetTime = UnixTimeStart + TimeSpan.FromSeconds (temp);
			}

			if (ApiLimitChanged != null) {
				try {
					ApiLimitChanged.BeginInvoke (this, EventArgs.Empty, null, null);
				} catch {}
			}

			return text;
		}

		string DownloadStringWithoutAuthentication (Uri uri, string method, byte[] postData)
		{
			WebHeaderCollection headers;
			return DownloadStringWithoutAuthentication (uri, method, postData, out headers);
		}

		string DownloadStringWithoutAuthentication (Uri uri, string method, byte[] postData, out WebHeaderCollection headers)
		{
			HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create (uri);
			if (method == HTTP_POST) {
				req.Method = method.ToString ();
				if (postData != null && postData.Length > 0) {
					req.ContentType = UrlEncodedMime;
					req.ContentLength = postData.Length;
					using (Stream strm = req.GetRequestStream ()) {
						strm.Write (postData, 0, postData.Length);
					}
				}
			}
			req.AllowAutoRedirect = false;
			req.UserAgent = UserAgent;

			using (HttpWebResponse response = (HttpWebResponse)req.GetResponse ()) {
				headers = response.Headers;
				using (StreamReader reader = new StreamReader (response.GetResponseStream (), Encoding.ASCII)) {
					return reader.ReadToEnd ();
				}
			}
		}
		#endregion

		#region INotifyPropertyChanged Members

		public event PropertyChangedEventHandler PropertyChanged;

		void InvokePropertyChanged (string name)
		{
			if (PropertyChanged != null)
				PropertyChanged (this, new PropertyChangedEventArgs (name));
		}

		#endregion
	}
}
