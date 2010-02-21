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
using System.Collections.ObjectModel;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public class TwitterTimeLine : ObservableCollection<Status>
	{
		HashSet<ulong> _ids = new HashSet<ulong> ();

		public new void Add (Status s)
		{
			if (!_ids.Add (s.ID))
				return;

			for (int i = 0; i < Count; i ++) {
				if (s.ID > this[i].ID) {
					InsertItem (i, s);
					return;
				}
			}
			base.Add (s);
		}

		public new void Insert (int idx, Status s)
		{
			throw new NotSupportedException ();
		}

		protected override void ClearItems ()
		{
			base.ClearItems ();
			_ids.Clear ();
		}

		protected override void RemoveItem (int index)
		{
			_ids.Remove (this[index].ID);
			base.RemoveItem (index);
		}

		protected override void SetItem (int index, Status item)
		{
			throw new NotSupportedException ();
		}
	}
}
