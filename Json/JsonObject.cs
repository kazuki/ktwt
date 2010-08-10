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
using System.Text;

namespace ktwt.Json
{
	public class JsonObject : JsonValue
	{
		Dictionary<string, JsonValue> _dic;

		public JsonObject (Dictionary<string, JsonValue> dic)
		{
			_dic = dic;
		}

		public override void ToJsonString (StringBuilder buffer)
		{
			int i = _dic.Count;
			buffer.Append ('{');
			foreach (KeyValuePair<string, JsonValue> pair in _dic) {
				buffer.Append ('\"');
				buffer.Append (JsonString.Encode (pair.Key));
				buffer.Append ("\":");
				pair.Value.ToJsonString (buffer);
				if (--i > 0)
					buffer.Append (',');
			}
			buffer.Append ('}');
		}

		public override JsonValueType ValueType {
			get { return JsonValueType.Object; }
		}

		public JsonValue this [string key] {
			get { return _dic[key]; }
		}

		public Dictionary<string, JsonValue> Value {
			get { return _dic; }
		}
	}
}
