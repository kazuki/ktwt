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
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ktwt.Json;
using ktwt.OAuth;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class MainWindow : Window
	{
		TwitterClient _client;
		Thread _streamingThrd, _restThrd;
		HashSet<ulong> _postIdSet = new HashSet<ulong> ();
		HashSet<ulong> _followingUserSet = new HashSet<ulong> ();
		string _friends;
		bool _firehoseMode = false;
		bool _trackMode = false;
		string _trackValues = "";
		bool _followingUserOnly = false;
		TimeSpan _restInterval = TimeSpan.FromSeconds (30);
		Status _replayInfo = null;
		string _replayName = null;
		string _screenName, _replyScreenName;
		ulong _userId;

		public MainWindow ()
		{
			OAuthClient oauthClient = new OAuthClient (ConsumerKeyStore.Key, ConsumerKeyStore.Secret, TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL);
			_client = new TwitterClient (oauthClient);
			_client.ApiLimitChanged += delegate (object sender, EventArgs e) {
				Dispatcher.Invoke (new EventHandler (delegate (object sender2, EventArgs e2) {
					apiLimitLabel.Text = _client.ApiLimitRemaining.ToString () + "/" + _client.ApiLimitMax.ToString () + "  ResetTime:" + _client.ApiLimitResetTime.ToLocalTime ().ToString ("HH時mm分");
				}), sender, e);
			};

			InitializeComponent ();

			ulong[] friends;
			User selfInfo;
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
					oauthClient.PasswordAuth (credentials.UserName, credentials.Password);
					friends = _client.GetFriendIDs ();
					selfInfo = _client.GetUserInfo (null, credentials.UserName);
					break;
				} catch {}
			}
			_trackValues = OAuthBase.UrlEncode (_trackValues);

			_userId = selfInfo.ID;
			_screenName = selfInfo.ScreenName;
			_replyScreenName = "@" + _screenName;

			StringBuilder sb = new StringBuilder ();
			for (int i = 0; i < Math.Min (399, friends.Length); i ++) {
				sb.Append (friends[i]);
				sb.Append (',');
			}
			sb.Append (_userId);
			for (int i = 0; i < friends.Length; i++)
				_followingUserSet.Add (friends[i]);
			if (friends.Length > 400)
				MessageBox.Show ("お友達が多すぎるので、400人分しか表示されません");
			_friends = sb.ToString ();

			if (_trackMode) {
				timelineContainer.Children.Remove (mentions);
				timelineContainer.ColumnDefinitions.RemoveAt (1);
			}

			AddStatus (new Status {Text = "Connecting...", User = new User {ScreenName = "System", Name = "System"}});
			_streamingThrd = new Thread (StreamingThread);
			_streamingThrd.IsBackground = true;
			_streamingThrd.Start ();

			_restThrd = new Thread (RestGetThread);
			_restThrd.IsBackground = true;
			_restThrd.Start ();

			if (!_trackMode) {
				Thread mentionThrd = new Thread (RestGetMentionsThread);
				mentionThrd.IsBackground = true;
				mentionThrd.Start ();
			}
		}

		class SystemInfo
		{
			public string Message { get; set; }
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
						Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {Text = "Initialized Twitter Streaming API", User = new User {ScreenName = "System", Name = "System"}});
						while (true) {
							try {
								line = ReadLineWithTimeout (state.Stream, ref buffer, ref filled, timeout);
								if (line == null) break;
								if (line.Length == 0) continue;
								JsonValueReader jsonReader = new JsonValueReader (line);
								JsonObject jsonRootObj = (JsonObject)jsonReader.Read ();
								if (jsonRootObj.Value.ContainsKey ("delete") || jsonRootObj.Value.ContainsKey ("limit"))
									continue;
								statuses[0] = JsonDeserializer.Deserialize<Status> (jsonRootObj);
								if (_followingUserOnly && !_followingUserSet.Contains (statuses[0].User.ID))
									continue;
								Dispatcher.Invoke (new AddStatusesDelegate (AddStatuses), (object)statuses);
							} catch {
								break;
							}
						}
					}
				} catch {}

				Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {Text = "Disconnected", User = new User {ScreenName = "System", Name = "System"}});
				if (failed == 0) {
					Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {Text = "Reconnecting...", User = new User {ScreenName = "System", Name = "System"}});
				} else {
					wait = wait + wait;
					if (wait > maxWait)
						wait = maxWait;
					Dispatcher.Invoke (new AddStatusDelegate (AddStatus), new Status {Text = "Waiting..." + ((int)wait.TotalSeconds).ToString() + "秒", User = new User {ScreenName = "System", Name = "System"}});
					Thread.Sleep (wait);
				}
				failed ++;
			}
		}

		void RestGetThread ()
		{
			ulong? since_id = null;
			const int count = 100;

			while (true) {
				try {
					Status[] statuses;
					if (_trackMode) {
						statuses = _client.Search (_trackValues, null, null, count, null, since_id, null, null);
					} else {
						statuses = _client.GetHomeTimeline (since_id, null, count, null);
					}
					since_id = TwitterClient.GetMaxStatusID (since_id, statuses);
					Dispatcher.Invoke (new AddStatusesDelegate (AddStatuses), (object)statuses);
				} catch {}
				Thread.Sleep (_restInterval);
			}
		}

		void RestGetMentionsThread ()
		{
			ulong? since_id = null;
			const int count = 100;

			while (true) {
				try {
					Status[] statuses = _client.GetMentions (since_id, null, count, null);
					since_id = TwitterClient.GetMaxStatusID (since_id, statuses);
					Dispatcher.Invoke (new AddStatusesDelegate (AddStatuses), (object)statuses);
				} catch {}
				Thread.Sleep (_restInterval + _restInterval);
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
				(timeline.ItemsSource as TwitterTimeLine).Add (status);
				if (!_trackMode && (status.InReplyToUserId == _userId || status.Text.Contains (_replyScreenName))) {
					(mentions.ItemsSource as TwitterTimeLine).Add (status);
				}
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
				case '/':
					url = "https://twitter.com" + e.Url;
					break;
				default:
					url = e.Url;
					break;
			}

			try {
				Process.Start (url);
			} catch {}
		}

		private void postTextBox_KeyDown (object sender, KeyEventArgs e)
		{
			if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.Return)
				postButton_Click (null, null);
		}

		private void postButton_Click (object sender, RoutedEventArgs e)
		{
			string txt = postTextBox.Text.Trim ();
			if (txt.Length == 0) return;
			postTextBox.IsReadOnly = true;
			postTextBox.Foreground = Brushes.DimGray;
			postButton.IsEnabled = false;
			if (_replayInfo != null && !txt.Contains (_replayName))
				ResetReplySetting (false);
			ThreadPool.QueueUserWorkItem (PostProcess, txt);
		}

		void PostProcess (object o)
		{
			string txt = (string)o;
			Status status = null;
			try {
				status = _client.Update (txt, (_replayInfo == null ? (ulong?)null : _replayInfo.ID), null, null);
			} catch {}
			Dispatcher.Invoke (new EndPostProcessDelegate (EndPostProcess), status);
		}

		delegate void EndPostProcessDelegate (Status status);
		void EndPostProcess (Status status)
		{
			postTextBox.IsReadOnly = false;
			postTextBox.Foreground = Brushes.White;
			postButton.IsEnabled = true;
			if (status != null) {
				ResetReplySetting (false);
				postTextBox.Text = "";
				postTextBox.Focus ();
				AddStatus (status);
			}
		}

		private void postTextBox_TextChanged (object sender, TextChangedEventArgs e)
		{
			int diff = TwitterClient.MaxStatusLength - postTextBox.Text.Length;
			postLengthText.Text = diff.ToString ();
			postLengthText.Foreground = (diff < 0 ? Brushes.Red : Brushes.White);
			if (_replayInfo != null) {
				if (postTextBox.Text.Contains (_replayName))
					SetReplySetting ();
				else
					ResetReplySetting (true);
			}
		}

		private void CheckBox_Checked (object sender, RoutedEventArgs e)
		{
			bool? v = (sender as CheckBox).IsChecked;
			if (v.HasValue)
				_followingUserOnly = v.Value;
		}

		private void ComboBox_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			_restInterval = TimeSpan.FromSeconds (((sender as ComboBox).SelectedIndex + 1) * 15);
		}

		private void TwitterStatusViewer_MouseDoubleClick (object sender, MouseButtonEventArgs e)
		{
			Status selected = (sender as TwitterStatusViewer).Status;
			if (selected.ID == 0 || selected.User.ScreenName == null)
				return;

			_replayInfo = selected;
			_replayName = "@" + selected.User.ScreenName;
			postTextBox.Text = _replayName + " ";
			SetReplySetting ();
			Dispatcher.BeginInvoke (new NotArgumentDelegate (delegate () {
				postTextBox.SelectionStart = postTextBox.Text.Length;
				postTextBox.Focus ();
			}));
		}
		delegate void NotArgumentDelegate ();

		void SetReplySetting ()
		{
			postButton.Content = "Reply";
		}

		void ResetReplySetting (bool btnTextOnly)
		{
			if (!btnTextOnly) {
				_replayInfo = null;
				_replayName = null;
			}
			postButton.Content = "Post";
		}
	}

	class TwitterTimeLine : ObservableCollection<Status>
	{
		public new void Add (Status s)
		{
			for (int i = 0; i < Count; i ++) {
				if (s.ID > this[i].ID) {
					InsertItem (i, s);
					return;
				}
			}
			base.Add (s);
		}

		public new void Insert (int idx, Status s)
		{
			throw new NotSupportedException ();
		}
	}
}
