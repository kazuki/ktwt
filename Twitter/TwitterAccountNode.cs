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
using System.Threading;
using ktwt.OAuth;
using ktwt.StatusStream;
using ktwt.Threading;

namespace ktwt.Twitter
{
	public class TwitterAccountNode : IStatusSource
	{
		OAuthClient _oauthClient;
		TwitterClient _client;
		List<RestStream> _restStreams = new List<RestStream>();
		RestStream[] _restStreamArray = new RestStream[0];

		public TwitterAccountNode (IntervalTimer timer)
		{
			_oauthClient = new OAuthClient (AppKeyStore.Key, AppKeyStore.Secret,
				TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL, TwitterClient.XAuthURL);
			_client = new TwitterClient (_oauthClient);

			timer.AddHandler (Run, TimeSpan.FromSeconds (1));
		}

		public IStatusStream[] OutputStreams {
			get { return _restStreamArray; }
		}

		public IStatusStream AddRestStream (RestUsage restUsage)
		{
			lock (_restStreams) {
				// 複数のHome/Mentions/DMsストリームの存在は許可しない。
				// すでにストリームがある場合は既存のストリームを返す
				if (restUsage.Type == RestType.Home || restUsage.Type == RestType.Mentions || restUsage.Type == RestType.DirectMessages) {
					for (int i = 0; i < _restStreams.Count; i ++) {
						if (_restStreams[i].Usage.Type == restUsage.Type)
							return _restStreams[i];
					}
				}

				RestStream strm = new RestStream (this, restUsage);
				_restStreams.Add (strm);
				_restStreamArray = _restStreams.ToArray ();
				return strm;
			}
		}

		public void RemoveRestStream (IStatusStream strm)
		{
			lock (_restStreams) {
				_restStreams.Remove (strm as RestStream);
				_restStreamArray = _restStreams.ToArray ();
			}
		}

		public string Name {
			get {
				string base_name = "Twitter";
				if (_oauthClient.Credentials is TwitterOAuthCredentialCache)
					base_name += ":" + (_oauthClient.Credentials as TwitterOAuthCredentialCache).ScreenName;
				return base_name;
			}
		}

		public Uri GetAuthorizeURL ()
		{
			return _oauthClient.GetAuthorizeURL ();
		}

		public void InputPin (string pin)
		{
			Dictionary<string, string> contents;
			System.Net.WebHeaderCollection headers;
			_oauthClient.InputPIN (pin, out contents, out headers);

			_oauthClient.Credentials = new TwitterOAuthCredentialCache (
				ulong.Parse (contents["user_id"]),
				contents["screen_name"],
				(OAuthCredentialCache)_oauthClient.Credentials);
		}

		public TwitterOAuthCredentialCache Credential {
			get { return (TwitterOAuthCredentialCache)_oauthClient.Credentials; }
			set { _oauthClient.Credentials = value; }
		}

		void Run ()
		{
			RestStream[] streams = _restStreamArray;
			for (int i = 0; i < streams.Length; i ++) {
				if (!streams[i].Usage.IsEnabled || streams[i].Usage.IsRunning || streams[i].Usage.NextExecTime > DateTime.Now)
					continue;
				streams[i].Usage.LastExecTime = DateTime.Now;
				ThreadPool.QueueUserWorkItem (streams[i].Run);
			}
		}

		private class RestStream : IStatusStream
		{
			public event EventHandler<StatusesArrivedEventArgs> StatusesArrived;

			public RestStream (TwitterAccountNode owner, RestUsage usage)
			{
				if (owner == null || usage == null)
					throw new ArgumentNullException ();

				Owner = owner;
				Usage = usage;
			}

			public TwitterAccountNode Owner { get; private set; }
			public RestUsage Usage { get; private set; }

			public string Name {
				get {
					string base_name = Owner.Name;
					switch (Usage.Type) {
						case RestType.Home: base_name += "'s home"; break;
						case RestType.Mentions: base_name += "'s mentions"; break;
						case RestType.DirectMessages: base_name += "'s dm"; break;
						case RestType.Search: base_name += " search \"hoge\""; break;
						case RestType.List: base_name += "'s list \"hoge\""; break;
					}
					return base_name;
				}
			}

			public void Run (object o)
			{
				if (StatusesArrived == null)
					return;

				Usage.IsRunning = true;
				try {
					Status[] status = null;
					switch (Usage.Type) {
						case RestType.Home:
							status = Owner._client.GetHomeTimeline (Usage.Since, null, Usage.Count, null);
							break;
						case RestType.Mentions:
							status = Owner._client.GetMentions (Usage.Since, null, Usage.Count, null);
							break;
						case RestType.DirectMessages:
							status = Owner._client.GetDirectMessagesAll (Usage.Since, null, Usage.Count, null);
							break;
					}
					if (status == null || status.Length == 0)
						return;

					StatusBase.SetSourceStream (status, this);
					Usage.Since = TwitterClient.GetMaxStatusID (Usage.Since, status);
					if (StatusesArrived != null)
						StatusesArrived (this, new StatusesArrivedEventArgs (status));
				} catch {
				} finally {
					Usage.IsRunning = false;
				}
			}
		}
	}
}
