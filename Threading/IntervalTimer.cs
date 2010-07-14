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

namespace ktwt.Threading
{
	public class IntervalTimer : IDisposable
	{
		Thread _schedulingThread;
		Thread[] _executeThreads;
		Dictionary<IntervalTimerDelegate, SchedulingInfo> _mapping = new Dictionary<IntervalTimerDelegate,SchedulingInfo> ();
		List<SchedulingInfo> _list = new List<SchedulingInfo> ();
		Queue<SchedulingInfo> _queue = new Queue<SchedulingInfo> ();
		ManualResetEvent _done = new ManualResetEvent (false); // _queueをロックしてSet/Reset
		bool _disposed = false;

		public IntervalTimer (TimeSpan precision, int number_of_execute_threads)
		{
			Precision = precision;
			NumberOfExecuteThreads = number_of_execute_threads;
			
			_schedulingThread = new Thread (SchedulingThread);
			_schedulingThread.Start();

			_executeThreads = new Thread[number_of_execute_threads];
			for (int i = 0; i < number_of_execute_threads; i ++) {
				_executeThreads[i] = new Thread (ExecuteThread);
				_executeThreads[i].Start ();
			}
		}

		public TimeSpan Precision { get; private set; }
		public int NumberOfExecuteThreads { get; private set; }

		public void AddHandler (IntervalTimerDelegate func, TimeSpan interval)
		{
			SchedulingInfo info = new SchedulingInfo (func, interval);
			lock (_mapping) {
				_mapping.Add (func, info);
			}
			Rescheduling (info);
		}

		public void RemoveHandler (IntervalTimerDelegate func)
		{
			SchedulingInfo info;
			lock (_mapping) {
				if (!_mapping.TryGetValue (func, out info))
					return;
				_mapping.Remove (func);
			}
			info.IsClosed = true;
			lock (_list) {
				_list.Remove (info);
			}
		}

		void SchedulingThread ()
		{
			List<SchedulingInfo> execs = new List<SchedulingInfo> ();
			while (!_disposed) {
				lock (_list) {
					for (int i = 0; i < _list.Count; i++) {
						if (_list[i].NextExecTime > DateTime.Now)
							break;
						execs.Add (_list[i]);
					}
					if (execs.Count > 0)
						_list.RemoveRange (0, execs.Count);
				}
				lock (_queue) {
					for (int i = 0; i < execs.Count; i ++)
						_queue.Enqueue (execs[i]);
					_done.Set ();
				}
				execs.Clear ();
				Thread.Sleep (Precision);
			}
		}

		void ExecuteThread ()
		{
			while (!_disposed) {
				if (!_done.WaitOne ())
					return;

				SchedulingInfo info = null;
				lock (_queue) {
					if (_queue.Count == 0) {
						_done.Reset ();
						continue;
					}
					info = _queue.Dequeue ();
				}

				info.Execute ();
				Rescheduling (info);
			}
		}

		void Rescheduling (SchedulingInfo info)
		{
			lock (_list) {
				if (info.IsClosed)
					return;

				for (int i = 0; i < _list.Count; i ++)
					if (_list[i].NextExecTime > info.NextExecTime) {
						_list.Insert (i, info);
						return;
					}
				_list.Add (info);
			}
		}

		public void Dispose()
		{
			_disposed = true;
			_done.Close ();
			for (int i = 0; i < _executeThreads.Length; i ++)
				_executeThreads[i].Join ();
			_schedulingThread.Join ();
		}

		class SchedulingInfo
		{
			public SchedulingInfo (IntervalTimerDelegate handler, TimeSpan interval)
			{
				Handler = handler;
				Interval = interval;
				NextExecTime = DateTime.Now;
				IsClosed = false;
			}

			public IntervalTimerDelegate Handler { get; private set; }
			public TimeSpan Interval { get; private set; }
			public DateTime NextExecTime { get; set; }
			public bool IsClosed { get; set; }

			public void Execute ()
			{
				DateTime startDt = DateTime.Now;
				NextExecTime = DateTime.MaxValue;
				try {
					Handler ();
				} catch {
				} finally {
					NextExecTime = startDt + Interval;
				}
			}
		}
	}
}
