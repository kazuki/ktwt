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
using System.IO;
using System.Text;
using System.Threading;
using ktwt.Json;
using ktwt.OAuth;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public class StreamingClient : IDisposable
	{
		const int MaxFollowCount = 400;
		static readonly Uri StreamingFilterUri = new Uri ("http://stream.twitter.com/1/statuses/filter.json");
		public event EventHandler<StatusArrivedEventArgs> StatusArrived;
		bool _active = true;
		IStreamingState _currentState;

		StreamingClient (TwitterAccount account)
		{
			Client = account.TwitterClient;
			IsConnecting = false;
			IsConnected = false;
			IsTrackMode = false;
			IsFollowMode = false;
			SearchKeywords = string.Empty;
		}

		public StreamingClient (TwitterAccount account, User[] friends) : this (account)
		{
			StringBuilder sb = new StringBuilder ();
			for (int i = 0; i < Math.Min (MaxFollowCount, friends.Length); i ++) {
				sb.Append (friends[i].ID);
				sb.Append (',');
			}
			if (sb.Length > 0)
				sb.Remove (sb.Length - 1, 1);

			StreamingUri = StreamingFilterUri;
			StreamingPostData = "follow=" + OAuthBase.UrlEncode (sb.ToString ());
			IsFollowMode = true;
			StreamingStart ();
		}

		public StreamingClient (TwitterAccount account, string searchKeywords) : this (account)
		{
			StreamingUri = StreamingFilterUri;
			StreamingPostData = "track=" + OAuthBase.UrlEncode (searchKeywords);
			IsTrackMode = true;
			SearchKeywords = searchKeywords;
			StreamingStart ();
		}

		static string ReadLineWithTimeout (Stream strm, ref byte[] buffer, ref int filled, TimeSpan timeout)
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

		void StreamingStart ()
		{
			Thread thrd = new Thread (StreamingThread);
			thrd.IsBackground = true;
			thrd.Start ();
		}

		void StreamingThread ()
		{
			string line = null;
			TimeSpan maxWait = TimeSpan.FromMinutes (1);
			TimeSpan startWait = TimeSpan.FromSeconds (2.5);
			TimeSpan wait = startWait;
			TimeSpan timeout = TimeSpan.FromSeconds (32);
			byte[] buffer = null;
			int filled = 0;
			while (_active) {
				try {
					IsConnecting = true;
					using (IStreamingState state = Client.StartStreaming (StreamingUri, "POST", StreamingPostData)) {
						_currentState = state;
						ReconnectCount = 0;
						IsConnecting = false;
						IsConnected = true;
						while (true) {
							try {
								line = ReadLineWithTimeout (state.Stream, ref buffer, ref filled, timeout);
								if (line == null) break;
								if (line.Length == 0) continue;
								JsonValueReader jsonReader = new JsonValueReader (line);
								JsonObject jsonRootObj = (JsonObject)jsonReader.Read ();
								if (jsonRootObj.Value.ContainsKey ("delete") || jsonRootObj.Value.ContainsKey ("limit"))
									continue;
								if (StatusArrived != null) {
									try {
										StatusArrived (this, new StatusArrivedEventArgs (JsonDeserializer.Deserialize<Status> (jsonRootObj)));
									} catch {}
								}
							} catch {
								break;
							}
						}
					}
				} catch {}

				_currentState = null;
				IsConnected = false;
				IsConnecting = false;

				if (ReconnectCount > 0) {
					wait = wait + wait;
					if (wait > maxWait)
						wait = maxWait;
					ReconnectTime = DateTime.Now + wait;
					Thread.Sleep (wait);
				}
				ReconnectCount++;
			}
		}

		public void Dispose ()
		{
			lock (this) {
				if (!_active)
					return;
				_active = false;
			}

			StatusArrived = null;
			if (_currentState != null) {
				ThreadPool.QueueUserWorkItem (delegate (object o) {
					try {
						(o as IDisposable).Dispose ();
					} catch {}
				}, _currentState);
			}
		}

		public TwitterClient Client { get; private set; }
		public bool IsConnected { get; private set; }
		public bool IsConnecting { get; private set; }
		public DateTime ReconnectTime { get; private set; }
		public int ReconnectCount { get; private set; }
		public Uri StreamingUri { get; private set; }
		public string StreamingPostData { get; private set; }
		public string SearchKeywords { get; private set; }
		public bool IsFollowMode { get; private set; }
		public bool IsTrackMode { get; private set; }
	}

	public class StatusArrivedEventArgs : EventArgs
	{
		public StatusArrivedEventArgs (Status status)
		{
			Status = status;
		}

		public Status Status { get; private set; }
	}
}
