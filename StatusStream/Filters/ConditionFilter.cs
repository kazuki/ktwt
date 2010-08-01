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

using System.Collections.Generic;

namespace ktwt.StatusStream.Filters
{
	public class ConditionFilter : StatusFilterBase
	{
		FilterEntry[] _filters = new FilterEntry[0];

		public ConditionFilter () : base ("Filter")
		{
		}

		protected override void FilterProcess (IStatusStream source, StatusBase s)
		{
			FilterEntry[] filters = _filters;
			for (int i = 0; i < filters.Length; i ++) {
				FilterEntry f = filters[i];
				if (!f.Condition.Evaluate (source, s))
					continue;
				for (int j = 0; j < f.Actions.Length; j ++) {
					ActionResult result = f.Actions[j].Execute (source, s);
					if (result == ActionResult.ContinueAction)
						continue;
					if (result == ActionResult.GotoNextFilter)
						break;
					if (result == ActionResult.ExitFilter)
						return;
					return; // Unknown Result
				}
			}
		}

		public FilterEntry[] Filters {
			get { return _filters; }
		}

		public void AddFilter (FilterEntry entry)
		{
			List<FilterEntry> list = new List<FilterEntry> (_filters);
			list.Add (entry);
			_filters = list.ToArray ();
		}

		public void RemoveFilter (FilterEntry entry)
		{
			List<FilterEntry> list = new List<FilterEntry> (_filters);
			list.Remove (entry);
			_filters = list.ToArray ();
		}
	}
}
