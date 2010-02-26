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
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Windows.Threading;
using ktwt.OAuth;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public class TwitterAccount : IUpdateChecker, IStreamingHandler
	{
		OAuthClient _oauthClient;
		TwitterClient _client;
		ICredentials _credential;

		StreamingClient _streamingClient = null;
		Dispatcher _dispatcher;

		RestUsage[] _restInfoList;
		ulong?[] _restSinceList;
		ulong _selfUserId = 0;

		public TwitterAccount ()
		{
			_oauthClient = new OAuthClient (ConsumerKeyStore.Key, ConsumerKeyStore.Secret,
				TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL);
			_client = new TwitterClient (_oauthClient);
			_dispatcher = Dispatcher.CurrentDispatcher;

			// defaults
			RestHome = new RestUsage {Interval = TimeSpan.FromSeconds (30), Count = 200, IsEnabled = true, LastExecTime = DateTime.MinValue};
			RestMentions = new RestUsage {Interval = TimeSpan.FromSeconds (600), Count = 20, IsEnabled = true, LastExecTime = DateTime.MinValue};
			RestDirectMessages = new RestUsage {Interval = TimeSpan.FromSeconds (600), Count = 20, IsEnabled = true, LastExecTime = DateTime.MinValue};
			_restInfoList = new RestUsage[] {RestHome, RestMentions, RestDirectMessages};
			_restSinceList = new ulong?[] {null, null, null};

			ThreadPool.QueueUserWorkItem (delegate (object o) {
				Self = _client.VerifyCredentials ();
				_selfUserId = Self.ID;
			});
		}

		public void UpdateOAuthAccessToken ()
		{
			if (_credential is NetworkCredential) {
				NetworkCredential nc = (NetworkCredential)_credential;
				_oauthClient.PasswordAuth (nc.UserName, nc.Password);
			} else {
				_oauthClient.UpdateAccessToken ();
			}
			_credential = (OAuthPasswordCache)_oauthClient.Credentials;
			ScreenName = ((OAuthPasswordCache)_credential).UserName;
		}

		delegate Status[] RestUpdateDelegate (ulong? since_id, ulong? max_id, int? count, int? page);
		public void UpdateTimeLines ()
		{
			RestUpdateDelegate[] funcs = new RestUpdateDelegate[] {_client.GetHomeTimeline, _client.GetMentions};
			for (int i = 0; i < funcs.Length; i ++) {
				RestUsage r = _restInfoList[i];

				if (!r.IsRunning && r.IsEnabled) {
					if (r.NextExecTime < DateTime.Now) {
						r.LastExecTime = DateTime.Now;
						r.IsRunning = true;
						ThreadPool.QueueUserWorkItem (delegate (object o) {
							int idx = (int)o;
							try {
								Status[] statuses = funcs[idx] (_restSinceList[idx], null, r.Count, null);
								_restSinceList[idx] = TwitterClient.GetMaxStatusID (_restSinceList[idx], statuses);
								_dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
									for (int j = 0; j < statuses.Length; j++)
										_restInfoList[idx].TimeLine.Add (statuses[j]);
								}));
							} catch {
							} finally {
								r.IsRunning = false;
								r.LastExecTime = DateTime.Now;
								r.UpdateNextExecTimeRemaining ();
							}
						}, i);
					}
					r.UpdateNextExecTimeRemaining ();
				}
			}
		}

		public TwitterClient TwitterClient {
			get { return _client; }
		}
		public ICredentials Credential {
			get { return _credential; }
			set {
				if (value is NetworkCredential) {
					_credential = (NetworkCredential)value;
					return;
				}
				_oauthClient.Credentials = value;
				_credential = value;
				ScreenName = ((OAuthPasswordCache)value).UserName;
			}
		}
		internal Dispatcher Dispatcher {
			get { return _dispatcher; }
		}
		public RestUsage RestHome { get; private set; }
		public RestUsage RestMentions { get; private set; }
		public RestUsage RestDirectMessages { get; private set; }
		public string ScreenName { get; private set; }
		public User Self { get; private set; }
		public bool IsIncludeOtherStatus { get; set; }
		public StreamingClient StreamingClient {
			get { return _streamingClient; }
			set {
				if (_streamingClient == value)
					return;
				if (_streamingClient != null)
					_streamingClient.Dispose ();
				_streamingClient = value;
				if (_streamingClient == null)
					return;
			}
		}

		void IStreamingHandler.Streaming_StatusArrived (object sender, StatusArrivedEventArgs e)
		{
			StreamingClient c = sender as StreamingClient;
			_dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				if (!IsIncludeOtherStatus) {
					if (!_client.FriendSet.Contains (e.Status.User.ID))
						return;
					if (e.Status.InReplyToUserId > 0) {
						if (!_client.FriendSet.Contains (e.Status.InReplyToUserId))
							return;
					}
					if (e.Status.RetweetedStatus != null && e.Status.RetweetedStatus.User != null) {
						if (e.Status.RetweetedStatus.User.ID == _selfUserId)
							return;
					}
				}
				HomeTimeline.Add (e.Status);
				if (ScreenName.Equals (e.Status.InReplyToScreenName) || e.Status.Text.Contains ("@" + ScreenName))
					Mentions.Add (e.Status);
			}));
		}

		public TwitterTimeLine HomeTimeline {
			get { return RestHome.TimeLine; }
		}
		public TwitterTimeLine Mentions {
			get { return RestMentions.TimeLine; }
		}
		public TwitterTimeLine DirectMessages {
			get { return RestDirectMessages.TimeLine; }
		}

		public class RestUsage : INotifyPropertyChanged
		{
			public event PropertyChangedEventHandler PropertyChanged;

			public RestUsage ()
			{
				IsRunning = false;
				Count = 0;
				TimeLine = new TwitterTimeLine ();
			}

			public TwitterTimeLine TimeLine { get; private set; }
			public TimeSpan Interval { get; set; }
			public bool IsEnabled { get; set; }
			public bool IsRunning { get; set; }
			public int Count { get; set; }
			public DateTime LastExecTime { get; set; }
			public DateTime NextExecTime {
				get { return LastExecTime + Interval; }
			}
			public TimeSpan NextExecTimeRemaining { get; private set; }

			public void UpdateNextExecTimeRemaining ()
			{
				NextExecTimeRemaining = NextExecTime - DateTime.Now;
				InvokePropertyChanged ("NextExecTimeRemaining");
			}

			void InvokePropertyChanged (string name)
			{
				if (PropertyChanged == null)
					return;

				try {
					PropertyChanged (this, new PropertyChangedEventArgs (name));
				} catch {}
			}
		}
	}
}
