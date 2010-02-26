﻿/*
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

		TwitterTimeLine _home = new TwitterTimeLine ();
		TwitterTimeLine _mentions = new TwitterTimeLine ();
		TwitterTimeLine _dms = new TwitterTimeLine ();
		StreamingClient _streamingClient = null;
		Dispatcher _dispatcher;

		ulong? _home_since_id = null, _mentions_since_id = null, _dms_since_id = null;

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

		public void UpdateTimeLines ()
		{
			if (!RestHome.IsRunning && RestHome.IsEnabled && RestHome.NextExecTime < DateTime.Now) {
				RestHome.LastExecTime = DateTime.Now;
				RestHome.IsRunning = true;
				ThreadPool.QueueUserWorkItem (delegate (object o) {
					try {
						Status[] statuses = _client.GetHomeTimeline (_home_since_id, null, RestHome.Count, null);
						_home_since_id = TwitterClient.GetMaxStatusID (_home_since_id, statuses);
						_dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
							for (int i = 0; i < statuses.Length; i ++)
								_home.Add (statuses[i]);
						}));
					} catch {
					} finally {
						RestHome.IsRunning = false;
					}
				});
			}
			if (!RestMentions.IsRunning && RestMentions.IsEnabled && RestMentions.NextExecTime < DateTime.Now) {
				RestMentions.LastExecTime = DateTime.Now;
				RestMentions.IsRunning = true;
				ThreadPool.QueueUserWorkItem (delegate (object o) {
					try {
						Status[] statuses = _client.GetMentions (_mentions_since_id, null, RestMentions.Count, null);
						_mentions_since_id = TwitterClient.GetMaxStatusID (_mentions_since_id, statuses);
						_dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
							for (int i = 0; i < statuses.Length; i ++)
								_mentions.Add (statuses[i]);
						}));
					} catch {
					} finally {
						RestMentions.IsRunning = false;
					}
				});
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
				}
				_home.Add (e.Status);
				if (ScreenName.Equals (e.Status.InReplyToScreenName) || e.Status.Text.Contains ("@" + ScreenName))
					_mentions.Add (e.Status);
			}));
		}

		public TwitterTimeLine HomeTimeline {
			get { return _home; }
		}
		public TwitterTimeLine Mentions {
			get { return _mentions; }
		}
		public TwitterTimeLine DirectMessages {
			get { return _dms; }
		}

		public class RestUsage
		{
			public RestUsage ()
			{
				IsRunning = false;
				Count = 0;
			}

			public TimeSpan Interval { get; set; }
			public bool IsEnabled { get; set; }
			public bool IsRunning { get; set; }
			public int Count { get; set; }
			public DateTime LastExecTime { get; set; }
			public DateTime NextExecTime {
				get { return LastExecTime + Interval; }
			}
		}
	}
}
