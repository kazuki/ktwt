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
using System.Net;
using System.Threading;
using System.Windows.Threading;
using ktwt.OAuth;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public class TwitterAccount : IUpdateChecker, IStreamingHandler
	{
		TwitterAccountManager _mgr;
		OAuthClient _oauthClient;
		TwitterClient _client;
		ICredentials _credential;

		StreamingClient _streamingClient = null;
		Dispatcher _dispatcher;

		RestUsage[] _restInfoList;
		ulong?[] _restSinceList;

		public TwitterAccount (TwitterAccountManager mgr)
		{
			_mgr = mgr;
			_oauthClient = new OAuthClient (ConsumerKeyStore.Key, ConsumerKeyStore.Secret, TwitterClient.RequestTokenURL,
				TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL, TwitterClient.XAuthURL);
			_client = new TwitterClient (_oauthClient);
			_dispatcher = Dispatcher.CurrentDispatcher;

			// defaults
			SelfUserID = 0;
			RestHome = new RestUsage {Interval = TimeSpan.FromSeconds (30), Count = 200, IsEnabled = true, LastExecTime = DateTime.MinValue};
			RestMentions = new RestUsage {Interval = TimeSpan.FromSeconds (600), Count = 20, IsEnabled = true, LastExecTime = DateTime.MinValue};
			RestDirectMessages = new RestUsage {Interval = TimeSpan.FromSeconds (600), Count = 20, IsEnabled = true, LastExecTime = DateTime.MinValue};
			_restInfoList = new RestUsage[] {RestHome, RestMentions, RestDirectMessages};
			_restSinceList = new ulong?[] {null, null, null};
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

		public void UpdateAllTimeLinesForce ()
		{
			for (int i = 0; i < _restInfoList.Length; i ++)
				_restInfoList[i].LastExecTime = DateTime.MinValue;
		}

		delegate Status[] RestUpdateDelegate (ulong? since_id, ulong? max_id, int? count, int? page);
		public void UpdateTimeLines ()
		{
			RestUpdateDelegate[] funcs = new RestUpdateDelegate[] {_client.GetHomeTimeline, _client.GetMentions, _client.GetDirectMessagesAll};
			for (int i = 0; i < funcs.Length; i ++) {
				RestUsage r = _restInfoList[i];

				if (!r.IsRunning && r.IsEnabled) {
					if (r.NextExecTime < DateTime.Now)
						UpdateTimeLine (r, funcs[i], (TimeLineType)i, i);
					r.UpdateNextExecTimeRemaining ();
				}
			}
		}
		void UpdateTimeLine (RestUsage r, RestUpdateDelegate func, TimeLineType type, int sinceListIndex)
		{
			r.LastExecTime = DateTime.Now;
			r.IsRunning = true;
			ThreadPool.QueueUserWorkItem (delegate (object o) {
				try {
					Status[] statuses = func (_restSinceList[sinceListIndex], null, r.Count, null);
					_restSinceList[sinceListIndex] = TwitterClient.GetMaxStatusID (_restSinceList[sinceListIndex], statuses);
					_dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
						for (int i = 0; i < statuses.Length; i++) {
							statuses[i].AccountInfo = this;
							if (type == TimeLineType.Home && IsMention (statuses[i])) {
								Mentions.Add (statuses[i]);
								if (!_mgr.HomeIncludeMentions)
									continue;
							} else if (type == TimeLineType.Mentions && _mgr.HomeIncludeMentions) {
								HomeTimeline.Add (statuses[i]);
							}
							r.TimeLine.Add (statuses[i]);
						}
					}));
				} catch {
				} finally {
					r.IsRunning = false;
					r.LastExecTime = DateTime.Now;
					r.UpdateNextExecTimeRemaining ();
				}
			});
		}
		enum TimeLineType
		{
			Home = 0,
			Mentions = 1,
			DirectMessage = 2
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
		public ulong SelfUserID { get; set; }
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

		bool IsMention (Status status)
		{
			if ((SelfUserID != 0 && status.InReplyToUserId == SelfUserID) || status.Text.Contains ("@" + ScreenName))
				return true;
			return false;
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
						if (e.Status.RetweetedStatus.User.ID == SelfUserID)
							return;
					}
				}
				e.Status.AccountInfo = this;
				if (IsMention (e.Status)) {
					Mentions.Add (e.Status);
					if (!_mgr.HomeIncludeMentions)
						return;
				}
				HomeTimeline.Add (e.Status);
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
