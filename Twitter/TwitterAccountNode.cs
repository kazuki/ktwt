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
using System.Threading;
using ktwt.Json;
using ktwt.OAuth;
using ktwt.StatusStream;
using ktwt.Threading;

namespace ktwt.Twitter
{
	public class TwitterAccountNode : IStatusSource, IDisposable
	{
		OAuthClient _oauthClient;
		TwitterClient _client;
		List<IStatusStream> _streams = new List<IStatusStream>();
		IStatusStream[] _streamArray = new IStatusStream[0];

		public TwitterAccountNode (IntervalTimer timer)
		{
			_oauthClient = new OAuthClient (AppKeyStore.Key, AppKeyStore.Secret,
				TwitterClient.RequestTokenURL, TwitterClient.AccessTokenURL, TwitterClient.AuthorizeURL, TwitterClient.XAuthURL);
			_client = new TwitterClient (_oauthClient);

			timer.AddHandler (Run, TimeSpan.FromSeconds (1));
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

		void AddStream (IStatusStream strm)
		{
			_streams.Add (strm);
			_streamArray = _streams.ToArray ();
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
			set { _oauthClient.Credentials = value; }
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

		private class StreamingStream : IStatusStream, IDisposable
		{
			public event EventHandler<StatusesArrivedEventArgs> StatusesArrived;
			IStreamingState _state;
			StreamingType _type;
			object[] _streamingArgs;
			Thread _thrd;
			bool _active = true;

			public StreamingStream (TwitterAccountNode owner, ulong[] follow, string[] track)
			{
				_type = StreamingType.Filter;
				_streamingArgs = new object[] {follow, track};
				Start (owner);
			}

			void Start (TwitterAccountNode owner)
			{
				Owner = owner;
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
						if (jsonRootObj.Value.ContainsKey ("delete") || jsonRootObj.Value.ContainsKey ("limit"))
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

			public string Name {
				get { return Owner.Name + "'s Streaming"; }
			}

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

			enum StreamingType
			{
				Filter,
				Sample,
			}
		}
	}
}
