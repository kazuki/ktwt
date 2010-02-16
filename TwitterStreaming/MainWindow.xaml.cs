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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using ktwt.Json;
using ktwt.OAuth;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class MainWindow : Window
	{
		OAuthClient _oauthClient;
		TwitterClient _client;
		Thread _streamingThrd, _restThrd;
		Dictionary<string, ImageSource> _imgCache = new Dictionary<string, ImageSource> ();
		HashSet<ulong> _postIdSet = new HashSet<ulong> ();
		string _friends;
		bool _firehoseMode = false;
		bool _trackMode = false;
		string _trackValues = "";

		public MainWindow ()
		{
			_oauthClient = new OAuthClient (ConsumerKeyStore.Key, ConsumerKeyStore.Secret, TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL);
			_client = new TwitterClient (_oauthClient);

			InitializeComponent ();

			string friends_text, userid;
			NetworkCredential credentials = null;
			while (true) {
				LoginWindow login = new LoginWindow ();
				if (credentials != null) {
					login.UserName = credentials.UserName;
					login.Password = credentials.Password;
					login.UseTrackMode = _trackMode;
					login.TrackWords = _trackValues;
				}
				login.ShowDialog ();
				credentials = new NetworkCredential (login.UserName, login.Password);
				_trackMode = login.UseTrackMode;
				_trackValues = login.TrackWords;
				try {
					_oauthClient.PasswordAuth (credentials.UserName, credentials.Password);
					friends_text = _oauthClient.DownloadString (new Uri ("https://twitter.com/friends/ids.json"), "GET", null);
					userid = _oauthClient.DownloadString (new Uri ("https://twitter.com/users/show/" + credentials.UserName + ".json"), "GET", null);
					break;
				} catch {}
			}
			_trackValues = System.Web.HttpUtility.UrlEncode (_trackValues, Encoding.UTF8);

			JsonValueReader reader;
			
			try {
				reader = new JsonValueReader (userid);
				userid = ((int)(((reader.Read () as JsonObject).Value["id"] as JsonNumber).Value)).ToString ();
			} catch {
				MessageBox.Show ("User-IDのパースに失敗");
				Close ();
				return;
			}

			try {
				reader = new JsonValueReader (friends_text);
				JsonArray friends = (JsonArray)reader.Read ();
				StringBuilder sb = new StringBuilder ();
				for (int i = 0; i < Math.Min (400, friends.Length); i ++) {
					sb.Append (((int)((friends[i] as JsonNumber).Value)).ToString ());
					sb.Append (',');
				}
				if (friends.Length < 400) {
					sb.Append (userid);
				} else {
					sb.Remove (sb.Length - 1, 1);
				}
				if (friends.Length > 400)
					MessageBox.Show ("お友達が多すぎるので、400人分しか表示されません");
				_friends = sb.ToString ();
			} catch {
				MessageBox.Show ("友達一覧のパースに失敗");
				Close ();
				return;
			}

			Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {ScreenName = "System", Name = "System", Text = "Connecting..."});
			_streamingThrd = new Thread (StreamingThread);
			_streamingThrd.IsBackground = true;
			_streamingThrd.Start ();

			_restThrd = new Thread (RestGetThread);
			_restThrd.IsBackground = true;
			_restThrd.Start ();
		}

		string ReadLineWithTimeout (Stream strm, ref byte[] buffer, ref int filled, TimeSpan timeout)
		{
			if (buffer == null) buffer = new byte[8192];
			while (true) {
				for (int i = 0; i < filled; i++) {
					if (buffer[i] == '\r' || buffer[i] == '\n') {
						string ret = Encoding.UTF8.GetString (buffer, 0, i);
						if (i + 1 < filled) {
							Array.Copy (buffer, i + 1, buffer, 0, filled - i - 1);
							filled -= i + 1;
						} else {
							filled = 0;
						}
						return ret.Trim ('\n', '\r', '\0');
					}
				}

				IAsyncResult ar = strm.BeginRead (buffer, filled, buffer.Length - filled, null, null);
				if (!ar.AsyncWaitHandle.WaitOne (timeout))
					throw new TimeoutException ();
				int read_size = strm.EndRead (ar);
				if (read_size <= 0)
					throw new IOException ();
				filled += read_size;
				if (buffer.Length == filled)
					Array.Resize<byte> (ref buffer, buffer.Length * 2);
			}
		}

		void StreamingThread ()
		{
			string url, postData = "";
			if (_firehoseMode) {
				url = "http://stream.twitter.com/1/statuses/firehose.json";
			} else {
				url = "http://stream.twitter.com/1/statuses/filter.json";
				if (_trackMode) {
					postData = "track=" + _trackValues;
				} else {
					postData = "follow=" + _friends;
				}
			}
			string line = null;
			int failed = 0;
			TimeSpan maxWait = TimeSpan.FromMinutes (1);
			TimeSpan startWait = TimeSpan.FromSeconds (2.5);
			TimeSpan wait = startWait;
			TimeSpan timeout = TimeSpan.FromSeconds (32);
			byte[] buffer = null;
			int filled = 0;
			Status[] statuses = new Status[1];
			while (true) {
				try {
					using (IStreamingState state = _client.StartStreaming (new Uri (url), "POST", postData)) {
						failed = 0;
						Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {ScreenName="System", Name="System", Text="Initialized Twitter Streaming API"});
						while (true) {
							try {
								line = ReadLineWithTimeout (state.Stream, ref buffer, ref filled, timeout);
								if (line == null) break;
								if (line.Length == 0) continue;
								JsonValueReader jsonReader = new JsonValueReader (line);
								JsonObject jsonRootObj = (JsonObject)jsonReader.Read ();
								if (jsonRootObj.Value.ContainsKey ("delete") || jsonRootObj.Value.ContainsKey ("limit"))
									continue;
								statuses[0] = new Status (jsonRootObj);
								Dispatcher.Invoke (new AddStatusesDelegate (AddStatuses), (object)statuses);
							} catch {
								break;
							}
						}
					}
				} catch {}

				Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {ScreenName = "System", Name = "System", Text = "Disconnected"});
				if (failed == 0) {
					Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {ScreenName = "System", Name = "System", Text = "Reconnecting..."});
				} else {
					wait = wait + wait;
					if (wait > maxWait)
						wait = maxWait;
					Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {ScreenName = "System", Name = "System", Text = "Waiting..." + ((int)wait.TotalSeconds).ToString() + "秒"});
					Thread.Sleep (wait);
				}
				failed ++;
			}
		}

		void RestGetThread ()
		{
			ulong since_id = 0;
			string base_url = "";
			List<Status> list = new List<Status> ();
			
			if (_trackMode) {
				base_url = "https://search.twitter.com/search.json?rpp=100&q=" + Uri.EscapeUriString (_trackValues.Replace (",", " OR "));
			} else {
				base_url = "https://api.twitter.com/1/statuses/home_timeline.json?count=100";
			}

			while (true) {
				list.Clear ();
				
				string url = base_url;
				if (since_id > 0) url += "&since_id=" + since_id.ToString ();

				try {
					if (_trackMode) {
						JsonArray array = (JsonArray)(((JsonObject)new JsonValueReader (_oauthClient.DownloadString (new Uri (url), "GET", null)).Read ()).Value["results"]);
						for (int i = 0; i < array.Length; i++) {
							JsonObject o = (JsonObject)array[i];
							Status status = new Status {
								ID = (ulong)(o.Value["id"] as JsonNumber).Value,
								ScreenName = "#" + (o.Value["from_user"] as JsonString).Value,
								Name = (o.Value["from_user"] as JsonString).Value,
								Text = (o.Value["text"] as JsonString).Value,
								ProfileImageUrl = (o.Value["profile_image_url"] as JsonString).Value
							};
							since_id = Math.Max (since_id, status.ID);
							list.Add (status);
						}
					} else {
						JsonArray array = (JsonArray)new JsonValueReader (_oauthClient.DownloadString (new Uri (url), "GET", null)).Read ();
						for (int i = 0; i < array.Length; i ++) {
							Status status = new Status ((JsonObject)array[i]);
							since_id = Math.Max (since_id, status.ID);
							list.Add (status);
						}
					}
				} catch {}

				list.Reverse ();
				Dispatcher.Invoke (new AddStatusesDelegate (AddStatuses), (object)list.ToArray ());
				Thread.Sleep (15 * 1000);
			}
		}

		delegate void AddStatusDelegate (Status status);
		void AddStatus (Status status)
		{
			AddStatuses (new Status[] {status});
		}

		delegate void AddStatusesDelegate (Status[] statuses);
		void AddStatuses (Status[] statuses)
		{
			foreach (Status status in statuses) {
				if (status.ID != 0 && !_postIdSet.Add (status.ID))
					continue;
				if (status.ProfileImageUrl != null) {
					lock (_imgCache) {
						status.TrySetProfileImage (_imgCache);
					}
				}
				(timeline.ItemsSource as TwitterTimeLine).Insert (0, status);
			}
		}

		private void TwitterStatusViewer_LinkClick (object sender, LinkClickEventArgs e)
		{
			string url;
			switch (e.Url[0]) {
				case '@':
					url = "https://twitter.com/" + e.Url.Substring (1);
					break;
				case '#':
					url = "https://search.twitter.com/search?q=" + Uri.EscapeUriString (e.Url.Substring (1));
					break;
				default:
					url = e.Url;
					break;
			}

			try {
				Process.Start (url);
			} catch {}
		}
	}

	class TwitterTimeLine : ObservableCollection<Status>
	{
	}
}
