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
		ulong? _since_id = null;
		TwitterAccount.RestUsage _rest;

		public SearchStatuses (TwitterAccount account, string keyword) : base ()
		{
			Account = account;
			Keyword = keyword;
			KeywordForRestAPI = keyword.Replace (",", " OR ").Replace ("  ", " ");
			_rest = account.RestSearch.CopyConfig ();
		}

		void IStreamingHandler.Streaming_StatusArrived (object sender, StatusArrivedEventArgs e)
		{
			StreamingClient c = sender as StreamingClient;
			Account.Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				Statuses.Add (e.Status);
			}));
		}

		public TwitterAccount Account { get; private set; }
		public string Keyword { get; private set; }
		public string KeywordForRestAPI { get; private set; }
		public TwitterTimeLine Statuses {
			get { return _rest.TimeLine; }
		}
		public TwitterAccount.RestUsage RestInfo {
			get { return _rest; }
		}

		public StreamingClient StreamingClient { get; set; }

		public void UpdateTimeLines ()
		{
			if (!_rest.IsRunning && _rest.IsEnabled) {
				if (_rest.NextExecTime < DateTime.Now) {
					_rest.LastExecTime = DateTime.Now;
					_rest.IsRunning = true;
					try {
						Status[] statuses = Account.TwitterClient.Search (KeywordForRestAPI, null, null, _rest.Count, null, _since_id, null, null);
						_since_id = TwitterClient.GetMaxStatusID (_since_id, statuses);
						Account.Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
							for (int i = 0; i < statuses.Length; i++)
								Statuses.Add (statuses[i]);
						}));
					} catch {
					} finally {
						_rest.IsRunning = false;
						_rest.LastExecTime = DateTime.Now;
						_rest.UpdateNextExecTimeRemaining ();
					}
				}
				_rest.UpdateNextExecTimeRemaining ();
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
