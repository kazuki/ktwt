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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ktwt.Json;
using ktwt.OAuth;
using ktwt.StatusStream;
using ktwt.Threading;
using ktwt.ui;

namespace ktwt.Twitter.ui
{
	public class TwitterAccountNode : IAccountInfo, IStatusSource, IDisposable
	{
		OAuthClient _oauthClient;
		TwitterClient _client;
		List<IStatusStream> _streams = new List<IStatusStream>();
		IStatusStream[] _streamArray = new IStatusStream[0];
		IntervalTimer _timer = null;

		public TwitterAccountNode ()
		{
			_oauthClient = new OAuthClient (AppKeyStore.Key, AppKeyStore.Secret,
				TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL, TwitterClient.XAuthURL);
			_client = new TwitterClient (_oauthClient);
			ID = SourceNodeInfo.SourceType + ":" + new Guid ().ToString (); // set dummy id

			AddUserStream ();
			AddRestStream (new RestUsage {Type=RestType.Home, Count=300, Interval=TimeSpan.FromSeconds (60), IsEnabled=true});
			AddRestStream (new RestUsage {Type=RestType.Mentions, Count=300, Interval=TimeSpan.FromSeconds (120), IsEnabled=true});
			AddRestStream (new RestUsage {Type=RestType.DirectMessages, Count=300, Interval=TimeSpan.FromSeconds (600), IsEnabled=true});
		}

		public OAuthClient OAuthClient {
			get { return _oauthClient; }
		}

		public TwitterClient TwitterClient {
			get { return _client; }
		}

		public IStatusStream[] OutputStreams {
			get { return _streamArray; }
		}

		public IStatusStream AddRestStream (RestUsage restUsage)
		{
			lock (_streams) {
				// 複数のHome/Mentions/DMsストリームの存在は許可しない。
				// すでにストリームがある場合は既存のストリームを返す
				if (restUsage.Type == RestType.Home || restUsage.Type == RestType.Mentions || restUsage.Type == RestType.DirectMessages) {
					for (int i = 0; i < _streams.Count; i ++) {
						RestStream restStrm = _streams[i] as RestStream;
						if (restStrm != null && restStrm.Usage.Type == restUsage.Type)
							return restStrm;
					}
				}

				RestStream strm = new RestStream (this, restUsage);
				_streams.Add (strm);
				_streamArray = _streams.ToArray ();
				return strm;
			}
		}

		public IStatusStream AddFilterStream (ulong[] follow, string[] track)
		{
			if (follow == null && track == null)
				throw new ArgumentNullException ();
			StreamingStream strm = new StreamingStream (this, follow, track);
			AddStream (strm);
			return strm;
		}

		public IStatusStream AddUserStream ()
		{
			return AddStream (new StreamingStream (this, StreamingStream.StreamingType.User));
		}

		public IStatusStream AddSampleStream ()
		{
			return AddStream (new StreamingStream (this, StreamingStream.StreamingType.Sample));
		}

		IStatusStream AddStream (IStatusStream strm)
		{
			_streams.Add (strm);
			_streamArray = _streams.ToArray ();
			return strm;
		}

		public void RemoveStream (IStatusStream strm)
		{
			lock (_streams) {
				_streams.Remove (strm);
				_streamArray = _streams.ToArray ();
			}
			if (strm is IDisposable)
				(strm as IDisposable).Dispose ();
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
			set {
				_oauthClient.Credentials = value;
				Summary = value.ScreenName;
				ID = SourceNodeInfo.SourceType + ":" + value.UserID.ToString ();
			}
		}

		public string Summary { get; private set; }

		public string ID { get; private set; }

		public IStatusSourceNodeInfo SourceNodeInfo {
			get { return TwitterNodeInfo.Instance; }
		}

		public void Start (IntervalTimer timer)
		{
			if (_timer != null || timer == null)
				return;

			_timer = timer;
			timer.AddHandler (Run, TimeSpan.FromSeconds (1));
			IStatusStream[] streams = _streamArray;
			for (int i = 0; i < streams.Length; i ++) {
				StreamingStream ss = streams[i] as StreamingStream;
				if (ss == null) continue;
				ss.Start (this);
			}
		}

		public bool Equals (IAccountInfo other)
		{
			TwitterAccountNode info = other as TwitterAccountNode;
			if (info == null)
				return false;
			return this.Credential.UserID == info.Credential.UserID;
		}

		void Run ()
		{
			IStatusStream[] streams = _streamArray;
			for (int i = 0; i < streams.Length; i ++) {
				RestStream restStrm = streams[i] as RestStream;
				if (restStrm == null) continue;
				if (!restStrm.Usage.IsEnabled || restStrm.Usage.IsRunning || restStrm.Usage.NextExecTime > DateTime.Now)
					continue;
				restStrm.Usage.LastExecTime = DateTime.Now;
				ThreadPool.QueueUserWorkItem (restStrm.Run);
			}
		}

		public void Dispose ()
		{
			IStatusStream[] streams = _streamArray;
			lock (_streams) {
				_streams.Clear ();
				_streamArray = new IStatusStream[0];
			}
			for (int i = 0; i < streams.Length; i ++) {
				if (streams[i] is IDisposable)
					(streams[i] as IDisposable).Dispose ();
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

				switch (Usage.Type) {
					case RestType.Home: Name = "Home"; break;
					case RestType.Mentions: Name = "Mentions"; break;
					case RestType.DirectMessages: Name = "DMs"; break;
					case RestType.Search: Name = "Search \"hoge\""; break;
					case RestType.List: Name = "List \"hoge\""; break;
					case RestType.UserTimeline: Name = "User TL"; break;
				}
			}

			public TwitterAccountNode Owner { get; private set; }
			public RestUsage Usage { get; private set; }

			public string Name { get; private set; }

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
						case RestType.UserTimeline:
							RestConfig.UserTimeline utConfig = (Usage.Config as RestConfig.UserTimeline);
							status = Owner._client.GetUserTimeline (utConfig.UserId, utConfig.ScreenName, Usage.Since, null, Usage.Count, null);
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

			public void ClearStatusesArrivedHandlers ()
			{
				StatusesArrived = null;
			}
		}

		private class StreamingStream : IStatusStream, IDisposable
		{
			public event EventHandler<StatusesArrivedEventArgs> StatusesArrived;
			IStreamingState _state;
			StreamingType _type;
			object[] _streamingArgs;
			Thread _thrd;
			bool _active = true;

			private StreamingStream (TwitterAccountNode owner)
			{
				Owner = owner;
			}

			public StreamingStream (TwitterAccountNode owner, ulong[] follow, string[] track) : this (owner)
			{
				_type = StreamingType.Filter;
				_streamingArgs = new object[] {follow, track};
				Name = "FilterStream";
			}

			public StreamingStream (TwitterAccountNode owner, StreamingType type) : this (owner)
			{
				_type = type;
				switch (type) {
					case StreamingType.User: Name = "UserStream"; break;
					case StreamingType.Sample: Name = "SampleStream"; break;
					case StreamingType.Filter: throw new ArgumentException ();
				}
			}

			public void Start (TwitterAccountNode owner)
			{
				_thrd = new Thread (StreamingThread);
				_thrd.Start ();
			}

			void StreamingThread ()
			{
				TimeSpan wait = TimeSpan.Zero;
				TimeSpan waitMin = TimeSpan.FromSeconds (1);
				TimeSpan waitMax = TimeSpan.FromSeconds (64);
				TimeSpan sleepUnit = TimeSpan.FromSeconds (0.1);

				while (_active) {
					DateTime waitStartTime = DateTime.Now;
					while (_active && DateTime.Now < waitStartTime + wait)
						Thread.Sleep (sleepUnit);
					if (!_active)
						break;
					if (wait.Ticks == 0)
						wait = waitMin;

					try {
						switch (_type) {
							case StreamingType.Filter:
								_state = Owner.TwitterClient.StartFilterStreaming ((ulong[])_streamingArgs[0], (string[])_streamingArgs[1]);
								break;
							case StreamingType.Sample:
								_state = Owner.TwitterClient.StartSampleStreaming ();
								break;
							case StreamingType.User:
								_state = Owner.TwitterClient.StartUserStreaming ();
								break;
							default:
								throw new ArgumentException ();
						}
						wait = waitMin;
					} catch {
						wait += wait;
						continue;
					}

					try {
						StreamingThread (_state.Stream);
					} catch {
						wait += wait;
						continue;
					} finally {
						try {
							if (_state != null)
								_state.Dispose ();
						} catch {}
						_state = null;
					}
				}
			}

			void StreamingThread (Stream strm)
			{
				byte[] buffer = null;
				string line;
				int filled = 0;
				TimeSpan timeout = TimeSpan.FromSeconds (32);

				while (_active) {
					try {
						line = ReadLineWithTimeout (strm, ref buffer, ref filled, timeout);
						if (line == null) break;
						if (line.Length == 0 || StatusesArrived == null) continue;
						JsonValueReader jsonReader = new JsonValueReader (line);
						JsonObject jsonRootObj = (JsonObject)jsonReader.Read ();
						if (jsonRootObj.Value.ContainsKey ("delete") || jsonRootObj.Value.ContainsKey ("limit") || jsonRootObj.Value.ContainsKey ("friends"))
							continue;
						try {
							Status s = JsonDeserializer.Deserialize<Status> (jsonRootObj);
							StatusesArrived (this, new StatusesArrivedEventArgs (s));
						} catch {}
					} catch {
						break;
					}
				}
			}

			static string ReadLineWithTimeout (Stream strm, ref byte[] buffer, ref int filled, TimeSpan timeout)
			{
				if (buffer == null) buffer = new byte[8192];
				while (true) {
					for (int i = 0; i < filled; i++) {
						if (buffer[i] == '\r' || buffer[i] == '\n') {
							string ret = System.Text.Encoding.UTF8.GetString (buffer, 0, i);
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

			public TwitterAccountNode Owner { get; private set; }

			public string Name { get; private set; }

			public void Dispose ()
			{
				if (!_active)
					return;
				_active = false;
				if (_state != null)
					_state.Dispose ();
				try {
					_thrd.Abort ();
				} catch {}
			}

			public void ClearStatusesArrivedHandlers ()
			{
				StatusesArrived = null;
			}

			public enum StreamingType
			{
				Filter,
				Sample,
				User,
			}
		}
	}
}
