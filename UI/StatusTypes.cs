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

namespace ktwt.ui
{
	static class StatusTypes
	{
		static readonly Dictionary<Type, IStatusRenderer> _renderers = new Dictionary<Type, IStatusRenderer> ();
		static readonly Dictionary<string, IStatusSourceNodeInfo> _typeNameMapping = new Dictionary<string, IStatusSourceNodeInfo> ();
		static List<string> _sources = new List<string> ();

		public static void Add (IStatusSourceNodeInfo info)
		{
			_renderers.Add (info.StatusType, info.Renderer);
			_typeNameMapping.Add (info.SourceType, info);
			_sources.Add (info.SourceType);
		}

		public static IStatusSourceNodeInfo GetInfo (string type_name)
		{
			return _typeNameMapping[type_name];
		}

		public static List<string> SourceTypes {
			get { return _sources; }
		}

		public static IStatusRenderer GetRenderer (Type statusType)
		{
			return _renderers[statusType];
		}

		public static Dictionary<string,string> SerializeAccountInfo (IAccountInfo info)
		{
			Dictionary<string, string> dic = info.SourceNodeInfo.SerializeAccountInfo (info);
			dic.Add ("type", info.SourceNodeInfo.SourceType);
			return dic;
		}

		public static IAccountInfo DeserializeAccountInfo (Dictionary<string, string> dic)
		{
			IStatusSourceNodeInfo info = _typeNameMapping[dic["type"]];
			return info.DeserializeAccountInfo (dic);
		}
	}
}
