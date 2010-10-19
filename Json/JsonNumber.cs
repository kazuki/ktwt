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

using System.Text;

namespace ktwt.Json
{
	public class JsonNumber : JsonValue
	{
		JsonNumberType _type;
		double _value;
		long _value_i64;
		ulong _value_ui64;

		public JsonNumber (JsonNumberType type, double value, long value_int64, ulong value_uint64)
		{
			_type = type;
			_value = value;
			if (type == JsonNumberType.Signed) {
				_value_i64 = value_int64;
			} else if (type == JsonNumberType.Unsigned) {
				_value_ui64 = value_uint64;
			}
		}

		public override void ToJsonString (StringBuilder buffer)
		{
			buffer.Append (_value.ToString ());
		}

		public override JsonValueType ValueType {
			get { return JsonValueType.Number; }
		}

		public JsonNumberType NumberType {
			get { return _type; }
		}

		public double Value {
			get { return _value; }
		}

		public long ValueSigned {
			get { return _value_i64; }
		}

		public ulong ValueUnsigned {
			get { return _value_ui64; }
		}
	}
}
