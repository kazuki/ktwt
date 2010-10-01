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
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;

namespace ktwt.Json
{
	public static class JsonDeserializer
	{
		public static T Deserialize<T> (string text) where T : class, new ()
		{
			return Deserialize<T> ((JsonObject)JsonValueReader.Read (text));
		}

		public static T Deserialize<T> (JsonObject obj) where T : class, new ()
		{
			return (T)Deserialize (obj, typeof (T));
		}

		static object Deserialize (JsonObject obj, Type t)
		{
			SerializationCache c = SerializationCache.Get (t);
			object r = FormatterServices.GetUninitializedObject (t);
			for (int i = 0; i < c.Attributes.Length; i ++) {
				JsonValue v;
				JsonObjectMappingAttribute att = c.Attributes[i];
				PropertyInfo prop = c.Properties[i];
				if (!obj.Value.TryGetValue (att.Key, out v) || v.ValueType != att.ValueType) continue;
				prop.SetValue (r, Deserialize (v, prop.PropertyType), null);
			}
			return r;
		}

		static object Deserialize (JsonValue v, Type t)
		{
			switch (v.ValueType) {
				case JsonValueType.Number:
					double d = (v as JsonNumber).Value;
					if (t == typeof (ulong))
						return (ulong)d;
					else if (t == typeof (long))
						return (long)d;
					else if (t == typeof (uint))
						return (uint)d;
					else if (t == typeof (int))
						return (int)d;
					else if (t == typeof (double))
						return (double)d;
					else if (t == typeof (float))
						return (float)d;
					break;
				case JsonValueType.String:
					if (t == typeof (DateTime))
						return DateTime.ParseExact ((v as JsonString).Value, SerializationCache.JsonDateTimeFormat, SerializationCache.InvariantCulture);
					else if (t.IsEnum)
						return Enum.Parse (t, (v as JsonString).Value, true);
					else
						return (v as JsonString).Value;
				case JsonValueType.Boolean:
					return (v as JsonBoolean).Value;
				case JsonValueType.Object:
					return Deserialize (v as JsonObject, t);
				case JsonValueType.Array:
					return Deserialize (v as JsonArray, t);
			}
			throw new NotSupportedException ();
		}

		static Array Deserialize (JsonArray jary, Type t)
		{
			t = t.GetElementType ();
			Array array = Array.CreateInstance (t, jary.Length);
			for (int i = 0; i < array.Length; i ++) {
				array.SetValue (Deserialize (jary[i], t), i);
			}
			return array;
		}
	}
}
