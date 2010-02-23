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
		bool _active = true;
		InternalState[] _states;

		StreamingClient (TwitterAccount[] accounts, IStreamingHandler target)
		{
			Accounts = new TwitterAccount[accounts.Length];
			_states = new InternalState[accounts.Length];
			for (int i = 0; i < accounts.Length; i ++) {
				Accounts[i] = accounts[i];
				Accounts[i].StreamingClient = this;
				_states[i] = new InternalState (accounts[i]);
			}
			SearchKeywords = string.Empty;
			Target = target;
		}

		public StreamingClient (TwitterAccount[] accounts, ulong[] friendIDs, IStreamingHandler target) : this (accounts, target)
		{
			ThreadPool.QueueUserWorkItem (delegate (object o) {
				string[] postDatas = new string[accounts.Length];
				for (int j = 0, p = 0; j < accounts.Length; j++, p = Math.Min (friendIDs.Length, p + MaxFollowCount) % friendIDs.Length) {
					StringBuilder sb = new StringBuilder ();
					for (int i = 0; i < Math.Min (MaxFollowCount, friendIDs.Length - p); i++) {
						sb.Append (friendIDs[i + p]);
						sb.Append (',');
					}
					if (sb.Length > 0)
						sb.Remove (sb.Length - 1, 1);
					_states[j].StreamingPostData = "follow=" + OAuthBase.UrlEncode (sb.ToString ());
				}
				StreamingUri = StreamingFilterUri;
				StreamingStart ();
			});
		}

		public StreamingClient (TwitterAccount[] accounts, string searchKeywords, IStreamingHandler target) : this (accounts, target)
		{
			StreamingUri = StreamingFilterUri;
			string postData = "track=" + OAuthBase.UrlEncode (searchKeywords);
			for (int i = 0; i < accounts.Length; i ++)
				_states[i].StreamingPostData = postData;
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
			for (int i = 0; i < _states.Length; i ++) {
				Thread thrd = new Thread (StreamingThread);
				thrd.IsBackground = true;
				thrd.Start (_states[i]);
			}
		}

		void StreamingThread (object o)
		{
			InternalState info = (InternalState)o;
			string line = null;
			TimeSpan maxWait = TimeSpan.FromMinutes (1);
			TimeSpan startWait = TimeSpan.FromSeconds (2.5);
			TimeSpan wait = startWait;
			TimeSpan timeout = TimeSpan.FromSeconds (32);
			byte[] buffer = null;
			int filled = 0;
			while (_active) {
				try {
					info.ConnectionState = StreamingState.Connecting;
					using (IStreamingState state = info.Account.TwitterClient.StartStreaming (StreamingUri, "POST", info.StreamingPostData)) {
						info.Handle = state;
						info.RetryCount = 0;
						info.ConnectionState = StreamingState.Connected;
						while (_active) {
							try {
								line = ReadLineWithTimeout (state.Stream, ref buffer, ref filled, timeout);
								if (line == null) break;
								if (line.Length == 0) continue;
								JsonValueReader jsonReader = new JsonValueReader (line);
								JsonObject jsonRootObj = (JsonObject)jsonReader.Read ();
								if (jsonRootObj.Value.ContainsKey ("delete") || jsonRootObj.Value.ContainsKey ("limit"))
									continue;
								try {
									Target.Streaming_StatusArrived (this, new StatusArrivedEventArgs (JsonDeserializer.Deserialize<Status> (jsonRootObj)));
								} catch {}
							} catch {
								break;
							}
						}
					}
				} catch {}
				if (!_active) break;

				info.Handle = null;
				info.ConnectionState = StreamingState.Disconnected;

				if (info.RetryCount > 0) {
					wait = wait + wait;
					if (wait > maxWait)
						wait = maxWait;
					info.NextRetryTime = DateTime.Now + wait;
					info.ConnectionState = StreamingState.Waiting;
					Thread.Sleep (wait);
				}
				info.RetryCount++;
			}
		}

		public void Dispose ()
		{
			lock (this) {
				if (!_active)
					return;
				_active = false;
			}

			Target = null;
			for (int i = 0; i < _states.Length; i ++) {
				if (_states[i].Handle != null) {
					ThreadPool.QueueUserWorkItem (delegate (object o) {
						try {
							(o as IDisposable).Dispose ();
						} catch {}
					}, _states[i].Handle);
				}
				if (Accounts[i].StreamingClient == this)
					Accounts[i].StreamingClient = null;
			}
		}

		public TwitterAccount[] Accounts { get; private set; }
		public InternalState[] States { get { return _states; } }
		public Uri StreamingUri { get; private set; }
		public string SearchKeywords { get; private set; }
		public IStreamingHandler Target { get; private set; }

		public class InternalState : INotifyPropertyChanged
		{
			StreamingState _state = StreamingState.Disconnected;
			int _retryCount = 0;
			DateTime _nextRetry = DateTime.MaxValue;

			public InternalState (TwitterAccount account)
			{
				Account = account;
			}

			public IStreamingState Handle { get; set; }
			public TwitterAccount Account { get; set; }
			public string StreamingPostData { get; set; }
			public StreamingState ConnectionState {
				get { return _state; }
				set {
					_state = value;
					InvokePropertyChanged ("ConnectionState");
				}
			}

			public DateTime NextRetryTime {
				get { return _nextRetry; }
				set {
					_nextRetry = value;
					InvokePropertyChanged ("NextRetryTime");
				}
			}
			public int RetryCount {
				get { return _retryCount; }
				set {
					_retryCount = value;
					InvokePropertyChanged ("RetryCount");
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;
			void InvokePropertyChanged (string name)
			{
				if (PropertyChanged != null)
					PropertyChanged (this, new PropertyChangedEventArgs (name));
			}
		}

		public enum StreamingState
		{
			Disconnected,
			Connecting,
			Connected,
			Waiting
		}
	}

	public class StatusArrivedEventArgs : EventArgs
	{
		public StatusArrivedEventArgs (Status status)
		{
			Status = status;
		}

		public Status Status { get; private set; }
	}

	public interface IStreamingHandler
	{
		void Streaming_StatusArrived (object sender, StatusArrivedEventArgs e);
	}
}
