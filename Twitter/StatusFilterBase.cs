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

namespace ktwt.Twitter
{
	public abstract class StatusFilterBase : IStatusSource
	{
		IStatusSource[] _sources = new IStatusSource[0];
		object _configRaw = null;
		public event EventHandler<StatusesArrivedEventArgs> StatusesArrived;

		#region Filter
		void Source_StatusesArrived (object sender, StatusesArrivedEventArgs e)
		{
			Status[] statuses = FilterProcess (e.Statuses);
			if (statuses == null || statuses.Length == 0)
				return;

			if (StatusesArrived != null)
				StatusesArrived (this, new StatusesArrivedEventArgs (statuses));
		}

		protected virtual Status[] FilterProcess (Status[] statuses)
		{
			List<Status> resultList = new List<Status> (statuses.Length);
			for (int i = 0; i < statuses.Length; i ++) {
				Status result = FilterProcess (statuses[i]);
				if (result != null)
					resultList.Add (result);
			}
			return resultList.ToArray ();
		}

		protected abstract Status FilterProcess (Status status);
		protected virtual void UpdateConfiguration (object newConfig)
		{
		}
		#endregion

		#region Public Members
		public IStatusSource[] StatusSources {
			get { return _sources; }
		}

		public void AddStatusSource (IStatusSource source)
		{
			source.StatusesArrived += Source_StatusesArrived;
			Array.Resize<IStatusSource> (ref _sources, _sources.Length + 1);
			_sources[_sources.Length - 1] = source;
		}

		public object Configuration {
			get { return _configRaw; }
			set {
				UpdateConfiguration (value);
				_configRaw = value;
			}
		}

		public abstract string Name { get; }

		public void Dispose ()
		{
			IStatusSource[] sources = _sources;
			_sources = null;
			if (sources != null) {
				for (int i = 0; i < sources.Length; i++)
					sources[i].StatusesArrived -= Source_StatusesArrived;
			}
		}
		#endregion
	}
}
