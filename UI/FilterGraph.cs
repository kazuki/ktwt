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
using System.Linq;
using System.Text;
using ktwt.StatusStream;

namespace ktwt.ui
{
	public class FilterGraph
	{
		public static void Construct (FilterGraphEdgeKey[] edges, Dictionary<FilterGraphNodeKey, INamedElement> nodes)
		{
			foreach (KeyValuePair<FilterGraphNodeKey, INamedElement> pair in nodes) {
				IStatusSource source = pair.Value as IStatusSource;
				IStatusViewer viewer = pair.Value as IStatusViewer;
				if (source != null) Reset (source.OutputStreams);
				if (viewer != null) Reset (viewer.InputStreams);
			}

			for (int i = 0; i < edges.Length; i ++) {
				FilterGraphEdgeKey e = edges[i];
				INamedElement se, de;
				if (!nodes.TryGetValue (e.SrcKey, out se) || !nodes.TryGetValue (e.DstKey, out de))
					continue;
				IStatusSource src = se as IStatusSource;
				IStatusViewer dst = de as IStatusViewer;
				if (src == null || dst == null || src.OutputStreams.Length <= e.SrcPinIndex)
					continue;
				dst.AddInputStream (src.OutputStreams[e.SrcPinIndex]);
			}
		}

		static void Reset (IStatusStream[] streams)
		{
			for (int i = 0; i < streams.Length; i ++)
				streams[i].ClearStatusesArrivedHandlers ();
		}
	}
}
