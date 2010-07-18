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

namespace ktwt.StatusStream.Filters
{
	public class ComplexCondition : ICondition
	{
		public ICondition[] Conditions { get; set; }
		public ComplexLogicType LogicType { get; set; }

		public bool Evaluate (IStatusStream source, StatusBase s)
		{
			ICondition[] list = Conditions;
			if (list == null || list.Length == 0)
				return false;

			switch (LogicType) {
				case ComplexLogicType.AND:
					for (int i = 0; i < list.Length; i++)
						if (!list[i].Evaluate (source, s))
							return false;
					return true;
				case ComplexLogicType.OR:
					for (int i = 0; i < list.Length; i++)
						if (list[i].Evaluate (source, s))
							return true;
					return false;
			}
			return false;
		}
	}
}
