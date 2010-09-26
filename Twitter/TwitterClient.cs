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
using ktwt.Net;
using ktwt.OAuth;

namespace ktwt.Twitter
{
	public class TwitterClient : INotifyPropertyChanged
	{
		public const int MaxStatusLength = 140;

		public static readonly Uri RequestTokenURL = new Uri ("https://twitter.com/oauth/request_token");
		public static readonly Uri AccessTokenURL = new Uri ("https://twitter.com/oauth/access_token");
		public static readonly Uri AuthorizeURL = new Uri ("https://twitter.com/oauth/authorize");
		public static readonly Uri XAuthURL = new Uri ("https://api.twitter.com/oauth/access_token");

		const string StatusesHomeTimelineURL = "https://api.twitter.com/1/statuses/home_timeline.json";
		const string StatusesMentionsURL = "https://twitter.com/statuses/mentions.json";
		const string StatusesShowURL = "https://api.twitter.com/1/statuses/show/{0}.json";
		const string StatusesUpdateURL = "https://twitter.com/statuses/update.json";
		const string StatusesDestroyURL = "https://api.twitter.com/1/statuses/destroy/{0}.json";
		const string StatusesRetweetURL = "https://api.twitter.com/1/statuses/retweet/{0}.json";
		const string SearchURL = "http://search.twitter.com/search.json";
		const string DirectMessagesURL = "http://api.twitter.com/1/direct_messages.json";
		const string DirectMessagesSentURL = "http://api.twitter.com/1/direct_messages/sent.json";
		const string DirectMessageNewURL = "http://api.twitter.com/1/direct_messages/new.json";
		static readonly Uri FriendIDsURL = new Uri ("https://twitter.com/friends/ids.json");
		const string UsersShowURL = "https://twitter.com/users/show.json";
		const string UserFriendsURL = "https://api.twitter.com/1/statuses/friends.json";
		const string UserFollowersURL = "https://api.twitter.com/1/statuses/followers.json";
		const string ListGetListURL = "https://api.twitter.com/1/{0}/lists.json";
		const string ListGetMembershipsListURL = "https://api.twitter.com/1/{0}/lists/memberships.json";
		const string ListGetSubscriptionsListURL = "https://api.twitter.com/1/{0}/lists/subscriptions.json";
		const string ListGetStatusesURL = "https://api.twitter.com/1/{0}/lists/{1}/statuses.json";
		const string ListGetMembersURL = "https://api.twitter.com/1/{0}/{1}/members.json";
		static readonly Uri AccountVerifyCredentialsURL = new Uri ("https://api.twitter.com/1/account/verify_credentials.json");
		const string FavoritesURL = "https://api.twitter.com/1/favorites.json";
		const string FavoritesUserURL = "https://api.twitter.com/1/favorites/{0}.json";
		const string FavoritesCreateURL = "https://api.twitter.com/1/favorites/create/{0}.json";
		const string FavoritesDestroyURL = "https://api.twitter.com/1/favorites/destroy/{0}.json";

		static readonly Uri StreamingFilterURL = new Uri ("http://stream.twitter.com/1/statuses/filter.json");
		static readonly Uri StreamingFirehoseURL = new Uri ("http://stream.twitter.com/1/statuses/firehose.json");
		static readonly Uri StreamingLinksURL = new Uri ("http://stream.twitter.com/1/statuses/links.json");
		static readonly Uri StreamingRetweetURL = new Uri ("http://stream.twitter.com/1/statuses/retweet.json");
		static readonly Uri StreamingSampleURL = new Uri ("http://stream.twitter.com/1/statuses/sample.json");

		static readonly Uri UserStreamingURL = new Uri ("https://betastream.twitter.com/2b/user.json");

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

		public TwitterClient (ISimpleWebClient baseClient)
		{
			_client = baseClient;
		}

		#region API Info
		public int ApiLimitMax {
			get { return _apiLimitMax; }
			private set {
				if (_apiLimitMax == value)
					return;
				_apiLimitMax = value;
				InvokePropertyChanged ("ApiLimitMax");
			}
		}

		public int ApiLimitRemaining {
			get { return _apiLimitRemaining; }
			private set {
				if (_apiLimitRemaining == value)
					return;
				_apiLimitRemaining = value;
				InvokePropertyChanged ("ApiLimitRemaining");
			}
		}

		public DateTime ApiLimitResetTime {
			get { return _apiLimitResetTime; }
			private set {
				if (_apiLimitResetTime.Equals (value))
					return;
				_apiLimitResetTime = value;
				InvokePropertyChanged ("ApiLimitResetTime");
			}
		}
		#endregion

		#region Timeline Methods
		public Status[] GetHomeTimeline (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetStatus (StatusesHomeTimelineURL, false, false, since_id, max_id, count, page);
		}

		public Status[] GetMentions (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetStatus (StatusesMentionsURL, false, false, since_id, max_id, count, page);
		}

		public Status[] GetStatus (string baseUrl, bool isDmMode, bool isListMode, ulong? since_id, ulong? max_id, int? count, int? page)
		{
			string query = "";
			if (since_id.HasValue) query += "&since_id=" + since_id.Value.ToString ();
			if (max_id.HasValue) query += "&max_id=" + max_id.Value.ToString ();
			if (count.HasValue) query += (isListMode ? "&per_page=" : "&count=") + count.Value.ToString ();
			if (page.HasValue) query += "&page=" + page.Value.ToString ();
			if (query.Length > 0) query = "?" + query.Substring (1);
			return GetStatus (new Uri (baseUrl + query), isDmMode);
		}

		Status[] GetStatus (Uri url, bool isDmMode)
		{
			string json = DownloadString (url, HTTP_GET, null);
			JsonArray array = (JsonArray)JsonValueReader.Read (json);
			Status[] statuses = new Status[array.Length];
			for (int i = 0; i < array.Length; i++) {
				JsonObject obj = (JsonObject)array[i];
				statuses[i] = JsonDeserializer.Deserialize<Status> (obj);
				if (isDmMode && obj.Value.ContainsKey ("sender"))
					SetDirectMessageInfo (statuses[i], obj);
			}
			return statuses;
		}

		void SetDirectMessageInfo (Status status, JsonObject dmRoot)
		{
			status.User = JsonDeserializer.Deserialize<User> ((JsonObject)dmRoot.Value["sender"]);
			User recipient = JsonDeserializer.Deserialize<User> ((JsonObject)dmRoot.Value["recipient"]);
			status.InReplyToScreenName = recipient.ScreenName;
			status.InReplyToUserId = recipient.ID;
		}
		#endregion

		#region Status Methods
		public Status Show (ulong id)
		{
			string json = DownloadString (new Uri (string.Format (StatusesShowURL, id)), HTTP_GET, null);
			return JsonDeserializer.Deserialize<Status> (json);
		}

		public Status Update (string status, ulong? in_reply_to_status_id, string geo_lat, string geo_long)
		{
			if (status == null || status.Length == 0 || status.Length > MaxStatusLength)
				throw new ArgumentException ();
			string query = "?status=" + OAuthBase.UrlEncode (status);
			if (in_reply_to_status_id.HasValue) query += "&in_reply_to_status_id=" + in_reply_to_status_id.Value.ToString ();
			if (geo_lat != null && geo_lat.Length > 0) query += "&lat=" + geo_lat;
			if (geo_long != null && geo_long.Length > 0) query += "&long=" + geo_long;
			string json = DownloadString (new Uri (StatusesUpdateURL + query), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> (json);
		}

		public Status Destroy (ulong id)
		{
			string json = DownloadString (new Uri (string.Format (StatusesDestroyURL, id)), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> (json);
		}

		public Status Retweet (ulong id)
		{
			string json = DownloadString (new Uri (string.Format (StatusesRetweetURL, id)), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> (json);
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
			return JsonDeserializer.Deserialize<User> (json);
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
			return GetAllPages<User> (url, query_base, "users", true);
		}
		#endregion

		#region List Methods
		ListInfo[] GetListInternal (string url, string screen_name_or_id)
		{
			return GetAllPages<ListInfo> (string.Format (url, screen_name_or_id), null, "lists", false);
		}

		public ListInfo[] GetList (string screen_name_or_id)
		{
			return GetListInternal (ListGetListURL, screen_name_or_id);
		}

		public ListInfo[] GetListSubscriptions (string screen_name_or_id)
		{
			return GetListInternal (ListGetSubscriptionsListURL, screen_name_or_id);
		}

		public ListInfo[] GetListMemberships (string screen_name_or_id)
		{
			return GetListInternal (ListGetMembershipsListURL, screen_name_or_id);
		}

		public Status[] GetListStatuses (ListInfo li, ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetListStatuses (li.User.ID.ToString (), li.ID, since_id, max_id, count, page);
		}

		public Status[] GetListStatuses (string screen_name_or_id, ulong list_id, ulong? since_id, ulong? max_id, int? count, int? page)
		{
			string base_url = string.Format (ListGetStatusesURL, screen_name_or_id, list_id);
			return GetStatus (base_url, false, true, since_id, max_id, count, page);
		}

		public User[] GetListMembers (ListInfo li)
		{
			return GetListMembers (li.User.ID.ToString (), li.ID);
		}

		public User[] GetListMembers (string screen_name_or_id, ulong list_id)
		{
			return GetAllPages<User> (string.Format (ListGetMembersURL, screen_name_or_id, list_id), null, "users", false);
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
			JsonObject rootObj = (JsonObject)JsonValueReader.Read (json);
			JsonArray array = (JsonArray)rootObj.Value["results"];
			Status[] statuses = new Status[array.Length];
			for (int i = 0; i < array.Length; i++) {
				JsonObject o = (JsonObject)array[i];
				statuses[i] = new Status {
					NumericID = (ulong)(o.Value["id"] as JsonNumber).Value,
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

		#region Direct Message Methods
		public Status[] GetDirectMessages (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetStatus (DirectMessagesURL, true, false, since_id, max_id, count, page);
		}
		public Status[] GetDirectSentMessages (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			return GetStatus (DirectMessagesSentURL, true, false, since_id, max_id, count, page);
		}
		public Status[] GetDirectMessagesAll (ulong? since_id, ulong? max_id, int? count, int? page)
		{
			List<Status> list = new List<Status> ();
			list.AddRange (GetDirectMessages (since_id, max_id, count, page));
			list.AddRange (GetDirectSentMessages (since_id, max_id, count, page));
			return list.ToArray ();
		}
		public Status SendDirectMessage (string screen_name, ulong? user_id, string text)
		{
			if (string.IsNullOrEmpty (screen_name) && (!user_id.HasValue || user_id.Value == 0))
				throw new ArgumentNullException ();
			string query;
			if (!string.IsNullOrEmpty (screen_name))
				query = "?screen_name=" + OAuthBase.UrlEncode (screen_name);
			else
				query = "?user_id=" + user_id.Value.ToString ();
			query += "&text=" + OAuthBase.UrlEncode (text);
			string json = DownloadString (new Uri (DirectMessageNewURL + query), HTTP_POST, null);
			JsonObject obj = (JsonObject)JsonValueReader.Read (json);
			Status status = JsonDeserializer.Deserialize<Status> (obj);
			SetDirectMessageInfo (status, obj);
			return status;
		}
		#endregion

		#region Social Graph Methods
		public ulong[] GetFriendIDs ()
		{
			string json = DownloadString (FriendIDsURL, HTTP_GET, null);
			JsonArray array = (JsonArray)JsonValueReader.Read (json);
			ulong[] friends = new ulong[array.Length];
			for (int i = 0; i < array.Length; i ++)
				friends[i] = (ulong)(array[i] as JsonNumber).Value;
			return friends;
		}
		#endregion

		#region Account Methods
		public User VerifyCredentials ()
		{
			string json = DownloadString (AccountVerifyCredentialsURL, HTTP_GET, null);
			return JsonDeserializer.Deserialize<User> (json);
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
			JsonArray array = (JsonArray)JsonValueReader.Read (json);
			Status[] status = new Status[array.Length];
			for (int i = 0; i < array.Length; i ++)
				status[i] = JsonDeserializer.Deserialize<Status> ((JsonObject)array[i]);
			return status;
		}

		Status FavoritesExec (string url, ulong id)
		{
			string json = DownloadString (new Uri (string.Format (url, id)), HTTP_POST, null);
			return JsonDeserializer.Deserialize<Status> (json);
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
		public IStreamingState StartFilterStreaming (ulong[] follow, string[] track)
		{
			StringBuilder sb = new StringBuilder ();
			if (follow != null && follow.Length > 0) {
				sb.Append ("follow=");
				sb.Append (follow[0]);
				for (int i = 1; i < follow.Length; i ++) {
					sb.Append (',');
					sb.Append (follow[i]);
				}
			}
			if (track != null && track.Length > 0) {
				if (sb.Length > 0) sb.Append ('&');
				sb.Append ("track=");
				sb.Append (Uri.EscapeDataString (track[0]));
				for (int i = 1; i < track.Length; i ++) {
					sb.Append (',');
					sb.Append (Uri.EscapeDataString (track[i]));
				}
			}

			// Twitter bug...?
			//return StartStreaming (StreamingFilterURL, HTTP_POST, sb.ToString ());

			return StartStreaming (new Uri (StreamingFilterURL.ToString () + "?" + sb.ToString ()), HTTP_GET, null);
		}

		public IStreamingState StartFirehoseStreaming ()
		{
			return StartStreaming (StreamingFirehoseURL, HTTP_GET, null);
		}

		public IStreamingState StartLinkStreaming ()
		{
			return StartStreaming (StreamingLinksURL, HTTP_GET, null);
		}

		public IStreamingState StartRetweetStreaming ()
		{
			return StartStreaming (StreamingRetweetURL, HTTP_GET, null);
		}

		public IStreamingState StartSampleStreaming ()
		{
			return StartStreaming (StreamingSampleURL, HTTP_GET, null);
		}

		public IStreamingState StartUserStreaming ()
		{
			return StartStreaming (UserStreamingURL, HTTP_GET, null);
		}

		IStreamingState StartStreaming (Uri uri, string method, string postData)
		{
			if (postData != null && postData.Length == 0) postData = null;
			HttpWebResponse res = _client.GetResponse (uri, method, (postData == null ? null : Encoding.ASCII.GetBytes (postData)));
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
		public static ulong? GetMaxStatusID (ulong? current, Status[] status)
		{
			if (status == null || status.Length == 0)
				return current;
			if (!current.HasValue)
				current = 0;
			ulong ret = GetMaxStatusID (current.Value, status);
			if (ret == 0)
				return null;
			return ret;
		}

		public static ulong GetMaxStatusID (ulong current, Status[] status)
		{
			for (int i = 0; i < status.Length; i ++)
				current = Math.Max (current, status[i].NumericID);
			return current;
		}

		public string SelfScreenName {
			get {
				if (_client.Credentials is NetworkCredential)
					return (_client.Credentials as NetworkCredential).UserName;
				if (_client.Credentials is OAuthPasswordCache)
					return (_client.Credentials as OAuthPasswordCache).UserName;
				if (_client.Credentials is TwitterOAuthCredentialCache)
					return (_client.Credentials as TwitterOAuthCredentialCache).ScreenName;
				throw new NotSupportedException ();
			}
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
			if (value != null && long.TryParse (value, out temp)) ApiLimitMax = (int)temp;
			value = headers[X_RateLimit_Remaining];
			if (value != null && long.TryParse (value, out temp)) ApiLimitRemaining = (int)temp;
			value = headers[X_RateLimit_Reset];
			if (value != null && long.TryParse (value, out temp)) ApiLimitResetTime = UnixTimeStart + TimeSpan.FromSeconds (temp);

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

		T[] GetAllPages<T> (string url, string query_base, string array_key, bool support_str_cursor) where T : class, new ()
		{
			if (query_base == null) query_base = string.Empty;
			query_base = (query_base.Length == 0 ? "?cursor=" : "&cursor=");
			string query = query_base + "-1";
			List<T> list = new List<T> ();
			while (true) {
				string json = DownloadString (new Uri (url + query), HTTP_GET, null);
				JsonObject obj = (JsonObject)JsonValueReader.Read (json);
				JsonArray array = (JsonArray)obj.Value[array_key];
				for (int i = 0; i < array.Length; i++)
					list.Add (JsonDeserializer.Deserialize<T> ((JsonObject)array[i]));

				string next = support_str_cursor
					? (obj.Value["next_cursor_str"] as JsonString).Value
					: ((ulong)(obj.Value["next_cursor"] as JsonNumber).Value).ToString ();
				if (next == "0")
					break;
				query = query_base + next;
			}
			return list.ToArray ();
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
