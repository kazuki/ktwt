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

namespace ktwt.Twitter.Graph
{
	public abstract class StatusFilterBase : StatusViewerBase, IStatusSource
	{
		protected IStatusStream[] _outputStreams = new IStatusStream[0];

		public IStatusStream[] OutputStreams {
			get { return _outputStreams; }
		}

		protected void InitOutputStreams (string[] names)
		{
			IStatusStream[] outputStreams = new IStatusStream[names.Length];
			for (int i = 0; i < outputStreams.Length; i ++)
				outputStreams[i] = new InternalStatusSource (names[i]);
			_outputStreams = outputStreams;
		}

		protected override void StatusesArrived_Handler (object sender, StatusesArrivedEventArgs e)
		{
			IStatusStream strm = sender as IStatusStream;
			for (int i = 0; i < e.Statuses.Length; i ++) {
				int ret = FilterProcess (strm, e.Statuses[i]);
				if (ret < 0 || ret >= OutputStreams.Length) continue;
				(OutputStreams[i] as InternalStatusSource).Raise (e.Statuses[i]);
			}
		}

		protected abstract int FilterProcess (IStatusStream source, Status s);

		class InternalStatusSource : IStatusStream
		{
			public event EventHandler<StatusesArrivedEventArgs> StatusesArrived;

			public InternalStatusSource (string name)
			{
				Name = name;
			}

			public void Raise (Status s)
			{
				Raise (new Status[] {s});
			}

			public void Raise (Status[] s)
			{
				if (StatusesArrived == null)
					return;
				StatusesArrived (this, new StatusesArrivedEventArgs (s));
			}

			public string Name { get; private set; }

			public void Dispose ()
			{
			}
		}
	}
}
