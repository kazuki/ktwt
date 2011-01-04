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

namespace ktwt.StatusStream
{
	public abstract class StatusFilterBase : StatusViewerBase, IStatusSource
	{
		protected IStatusStream[] _outputStreams = new IStatusStream[0];

		protected StatusFilterBase (string name) : base (name)
		{
		}

		public IStatusStream[] OutputStreams {
			get { return _outputStreams; }
		}

		public void InitOutputStreams (string[] names)
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
				FilterProcess (strm, e.Statuses[i]);
			}
		}

		public void Output (IStatusStream output, StatusBase s)
		{
			(output as InternalStatusSource).Raise (s);
		}

		protected abstract void FilterProcess (IStatusStream source, StatusBase s);

		protected class InternalStatusSource : IStatusStream
		{
			public event EventHandler<StatusesArrivedEventArgs> StatusesArrived;

			public InternalStatusSource (string name)
			{
				Name = name;
			}

			public void Raise (StatusBase s)
			{
				Raise (new StatusBase[] {s});
			}

			public void Raise (StatusBase[] s)
			{
				if (StatusesArrived == null)
					return;
				StatusesArrived (this, new StatusesArrivedEventArgs (s));
			}

			public string Name { get; private set; }

			public void Dispose ()
			{
			}

			public void ClearStatusesArrivedHandlers ()
			{
				StatusesArrived = null;
			}
		}
	}
}
