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

namespace ktwt.Twitter
{
	public class RestUsage : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public RestUsage ()
		{
		}

		RestType _restType = RestType.Home;
		public RestType Type {
			get { return _restType; }
			set {
				_restType = value;
				InvokePropertyChanged ("Type");
			}
		}

		object _config = null;
		public object Config {
			get { return _config; }
			set {
				_config = value;
				InvokePropertyChanged ("Config");
			}
		}

		TimeSpan _interval = TimeSpan.MaxValue;
		public TimeSpan Interval {
			get { return _interval; }
			set {
				_interval = value;
				InvokePropertyChanged ("Interval");
			}
		}

		bool _enabled = false;
		public bool IsEnabled {
			get { return _enabled; }
			set {
				_enabled = value;
				InvokePropertyChanged ("IsEnabled");
			}
		}

		bool _isRunning = false;
		public bool IsRunning {
			get { return _isRunning; }
			set {
				_isRunning = value;
				InvokePropertyChanged ("IsRunning");
			}
		}

		int _count = 0;
		public int Count {
			get { return _count; }
			set {
				_count = value;
				InvokePropertyChanged ("Count");
			}
		}

		ulong? _since = null;
		public ulong? Since {
			get { return _since; }
			set {
				_since = value;
				InvokePropertyChanged ("Since");
			}
		}

		DateTime _lastExec = DateTime.MinValue;
		public DateTime LastExecTime {
			get { return _lastExec; }
			set {
				_lastExec = value;
				InvokePropertyChanged ("LastExecTime");
			}
		}

		public DateTime NextExecTime {
			get { return LastExecTime + Interval; }
		}
		public TimeSpan NextExecTimeRemaining { get; private set; }

		public void UpdateNextExecTimeRemaining ()
		{
			NextExecTimeRemaining = NextExecTime - DateTime.Now;
			InvokePropertyChanged ("NextExecTimeRemaining");
		}

		void InvokePropertyChanged (string name)
		{
			if (PropertyChanged == null)
				return;
			try {
				PropertyChanged (this, new PropertyChangedEventArgs (name));
			} catch {}
		}
	}
}
