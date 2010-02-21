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
using ktwt.Twitter;

namespace TwitterStreaming
{
	public class SearchStatuses : IUpdateChecker, IDisposable, IStreamingHandler
	{
		TwitterTimeLine _timeline = new TwitterTimeLine ();
		ulong? _since_id = null;
		delegate void EmptyDelegate ();

		public SearchStatuses (TwitterAccount account, string keyword) : base ()
		{
			Account = account;
			Keyword = keyword;

			// default
			IsEnabled = true;
			Interval = TimeSpan.FromSeconds (180);
			LastExecTime = DateTime.MinValue;
			Count = 100;
		}

		void IStreamingHandler.Streaming_StatusArrived (object sender, StatusArrivedEventArgs e)
		{
			StreamingClient c = sender as StreamingClient;
			Account.Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				_timeline.Add (e.Status);
			}));
		}

		public TwitterAccount Account { get; private set; }
		public string Keyword { get; private set; }
		public TwitterTimeLine Statuses {
			get {
				return _timeline;
			}
		}

		public TimeSpan Interval { get; set; }
		public bool IsEnabled { get; set; }
		public int Count { get; set; }
		public DateTime LastExecTime { get; private set; }
		public DateTime NextExecTime {
			get { return LastExecTime + Interval; }
		}
		public StreamingClient StreamingClient { get; set; }

		public void UpdateTimeLines ()
		{
			if (IsEnabled && NextExecTime < DateTime.Now) {
				LastExecTime = DateTime.Now;
				try {
					Status[] statuses = Account.TwitterClient.Search (Keyword, null, null, Count, null, _since_id, null, null);
					_since_id = TwitterClient.GetMaxStatusID (_since_id, statuses);
					Account.Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
						for (int i = 0; i < statuses.Length; i++)
							_timeline.Add (statuses[i]);
					}));
				} catch {}
			}
		}

		public void Dispose ()
		{
			if (StreamingClient != null) {
				StreamingClient.Dispose ();
				StreamingClient = null;
			}
		}
	}
}
