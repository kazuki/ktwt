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

using System.Text;

namespace ktwt.Json
{
	public class JsonString : JsonValue
	{
		string _value;

		public JsonString (string value)
		{
			_value = value;
		}

		public override void ToJsonString (StringBuilder buffer)
		{
			buffer.Append ('\"');
			buffer.Append (Encode (_value));
			buffer.Append ('\"');
		}

		public override JsonValueType ValueType {
			get { return JsonValueType.String; }
		}

		public string Value {
			get { return _value; }
		}

		public static string Encode (string text)
		{
			return text.Replace (@"\", @"\\").Replace ("\"", "\\\"")
				.Replace ("\t", "\\t").Replace ("\b", "\\b").Replace ("\f", "\\f")
				.Replace ("\n", "\\n").Replace ("\r", "\\r");
		}
	}
}
