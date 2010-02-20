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

namespace TwitterStreaming
{
	public class TwitterAccountManager
	{
		object _lock = new object ();
		TwitterAccount[] _accounts;
		List<SearchStatuses> _searches = new List<SearchStatuses> ();
		Thread _restThread;

		public TwitterAccountManager ()
		{
			_accounts = new TwitterAccount[0];
			_restThread = new Thread (RestThread);
			_restThread.IsBackground = true;
			_restThread.Start ();
		}

		public void UpdateAccounts (TwitterAccount[] accounts)
		{
			lock (_lock) {
				_accounts = accounts;
			}
		}

		public TwitterAccount[] Accounts {
			get { return _accounts; }
		}

		public List<SearchStatuses> Searches {
			get { return _searches; }
		}

		void RestThread ()
		{
			while (true) {
				List<IUpdateChecker> list = new List<IUpdateChecker> ();
				lock (_lock) {
					list.AddRange (_accounts);
				}
				lock (_searches) {
					for (int i = 0; i < _searches.Count; i ++)
						list.Add (_searches[i]);
				}
				for (int i = 0; i < list.Count; i++) {
					try {
						list[i].UpdateTimeLines ();
					} catch {}
				}
				Thread.Sleep (1000);
			}
		}
	}
}
