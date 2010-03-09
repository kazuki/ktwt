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
	public class ListStatuses : IUpdateChecker, IDisposable, IStreamingHandler
	{
		ulong? _since_id = null;

		public ListStatuses (TwitterAccount account, ListInfo li) : base ()
		{
			Account = account;
			List = li;
			RestInfo = account.RestList.CopyConfig ();
		}

		void IStreamingHandler.Streaming_StatusArrived (object sender, StatusArrivedEventArgs e)
		{
			StreamingClient c = sender as StreamingClient;
			Account.Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
				Statuses.Add (e.Status);
			}));
		}

		public void UpdateTimeLines ()
		{
			if (!RestInfo.IsRunning && RestInfo.IsEnabled) {
				if (RestInfo.NextExecTime < DateTime.Now) {
					RestInfo.LastExecTime = DateTime.Now;
					RestInfo.IsRunning = true;
					try {
						Status[] statuses = Account.TwitterClient.GetListStatuses (List, _since_id, null, RestInfo.Count, null);
						_since_id = TwitterClient.GetMaxStatusID (_since_id, statuses);
						Account.Dispatcher.BeginInvoke (new EmptyDelegate (delegate () {
							for (int i = 0; i < statuses.Length; i++)
								Statuses.Add (statuses[i]);
						}));
					} catch {
					} finally {
						RestInfo.IsRunning = false;
						RestInfo.LastExecTime = DateTime.Now;
						RestInfo.UpdateNextExecTimeRemaining ();
					}
				}
				RestInfo.UpdateNextExecTimeRemaining ();
			}
		}

		public void Dispose ()
		{
			if (StreamingClient != null) {
				StreamingClient.Dispose ();
				StreamingClient = null;
			}
		}

		public TwitterAccount Account { get; private set; }
		public ListInfo List { get; private set; }
		public TwitterTimeLine Statuses { get { return RestInfo.TimeLine; } }
		public TwitterAccount.RestUsage RestInfo { get; private set; }
		public StreamingClient StreamingClient { get; set; }
	}
}
