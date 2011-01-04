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
using ktwt.StatusStream;

namespace ktwt.ui
{
	public class FilterGraphEdgeKey : IEquatable<FilterGraphEdgeKey>
	{
		public FilterGraphNodeKey SrcKey { get; set; }
		public int SrcPinIndex { get; set; }
		public FilterGraphNodeKey DstKey { get; set; }
		public int DstPinIndex { get; set; }

		public static Dictionary<string, string> Serialize (FilterGraphEdgeKey x)
		{
			Dictionary<string, string> d = new Dictionary<string,string> ();
			d.Add ("type0", x.SrcKey.Type.ToString ());
			d.Add ("key0", x.SrcKey.Key);
			d.Add ("pin0", x.SrcPinIndex.ToString ());
			d.Add ("type1", x.DstKey.Type.ToString ());
			d.Add ("key1", x.DstKey.Key);
			d.Add ("pin1", x.DstPinIndex.ToString ());
			return d;
		}

		public static FilterGraphEdgeKey Deserialize (Dictionary<string, string> x)
		{
			return new FilterGraphEdgeKey {
				SrcKey = new FilterGraphNodeKey (x["type0"], x["key0"]),
				SrcPinIndex = int.Parse (x["pin0"]),
				DstKey = new FilterGraphNodeKey (x["type1"], x["key1"]),
				DstPinIndex = int.Parse (x["pin1"])
			};
		}

		public override int GetHashCode ()
		{
			return SrcKey.GetHashCode () ^ SrcPinIndex ^ DstKey.GetHashCode () ^ DstPinIndex;
		}

		public override bool Equals (object obj)
		{
			return Equals ((FilterGraphEdgeKey)obj);
		}

		public bool Equals (FilterGraphEdgeKey other)
		{
			return this.SrcKey.Equals (other.SrcKey) &&
				this.SrcPinIndex == other.SrcPinIndex &&
				this.DstKey.Equals (other.DstKey) &&
				this.DstPinIndex == other.DstPinIndex;
		}
	}
}
