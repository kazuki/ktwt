﻿/*
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
	public enum ActionResult
	{
		/// <summary>後続のActionを実行</summary>
		ContinueAction,

		/// <summary>後続のActionが合っても次のフィルタに移動</summary>
		GotoNextFilter,

		/// <summary>後続のActionやフィルタが合っても全フィルタ処理を終了する</summary>
		ExitFilter
	}
}
