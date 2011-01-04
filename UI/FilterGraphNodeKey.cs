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
using ktwt.StatusStream;

namespace ktwt.ui
{
	public class FilterGraphNodeKey : IEquatable<FilterGraphNodeKey>
	{
		public FilterGraphNodeKey (string type, string key)
			: this ((ElementType)Enum.Parse (typeof (ElementType), type), key)
		{
		}

		public FilterGraphNodeKey (ElementType type, string key)
		{
			this.Type = type;
			this.Key = key;
		}

		public ElementType Type { get; set; }
		public string Key { get; set; }

		public override int GetHashCode ()
		{
			return this.Type.GetHashCode () ^ this.Key.GetHashCode ();
		}

		public override bool Equals (object obj)
		{
			if (obj is FilterGraphNodeKey)
				return Equals ((FilterGraphNodeKey)obj);
			return false;
		}

		public bool Equals (FilterGraphNodeKey other)
		{
			return (this.Type == other.Type) && this.Key.Equals (other.Key);
		}
	}
}
