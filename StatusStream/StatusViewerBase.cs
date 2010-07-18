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

namespace ktwt.StatusStream
{
	public abstract class StatusViewerBase : IStatusViewer
	{
		protected IStatusStream[] _inputStreams = new IStatusStream[0];

		protected StatusViewerBase (string name)
		{
			Name = name;
		}

		public void AddInputStream (IStatusSource source)
		{
			foreach (IStatusStream strm in source.OutputStreams)
				AddInputStream (strm);
		}

		public void AddInputStream (IStatusStream strm)
		{
			List<IStatusStream> list = new List<IStatusStream> (_inputStreams);
			if (list.Contains (strm))
				throw new ArgumentException ();
			list.Add (strm);
			strm.StatusesArrived += StatusesArrived_Handler;
			_inputStreams = list.ToArray ();
		}

		public void RemoveInputStream (IStatusSource source)
		{
			foreach (IStatusStream strm in source.OutputStreams)
				RemoveInputStream (strm);
		}

		public void RemoveInputStream (IStatusStream strm)
		{
			List<IStatusStream> list = new List<IStatusStream> (_inputStreams);
			list.Remove (strm);
			strm.StatusesArrived -= StatusesArrived_Handler;
			_inputStreams = list.ToArray ();
		}

		public IStatusStream[] InputStreams {
			get { return _inputStreams; }
		}

		public string Name { get; protected set; }

		protected abstract void StatusesArrived_Handler (object sender, StatusesArrivedEventArgs e);
	}
}
