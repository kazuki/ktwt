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
		static readonly Type MappingAttributeType = typeof (JsonObjectMappingAttribute);
		static Dictionary<Type, CacheEntry> _reflectionCache = new Dictionary<Type, CacheEntry> ();
		public const string DateTimeFormat = "ddd MMM dd HH:mm:ss zzzz yyyy";
		public static IFormatProvider InvariantCulture = CultureInfo.InvariantCulture;

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
			CacheEntry c;
			lock (_reflectionCache) {
				if (!_reflectionCache.TryGetValue (t, out c)) {
					c = new CacheEntry (t);
					_reflectionCache.Add (t, c);
				}
			}

			object r = FormatterServices.GetUninitializedObject (t);
			for (int i = 0; i < c.Attributes.Length; i ++) {
				JsonValue v;
				JsonObjectMappingAttribute att = c.Attributes[i];
				PropertyInfo prop = c.Properties[i];
				if (!obj.Value.TryGetValue (att.Key, out v) || v.ValueType != att.ValueType) continue;
				switch (att.ValueType) {
					case JsonValueType.Number:
						double d = (v as JsonNumber).Value;
						if (prop.PropertyType == typeof (ulong))
							prop.SetValue (r, (ulong)d, null);
						else if (prop.PropertyType == typeof (long))
							prop.SetValue (r, (long)d, null);
						else if (prop.PropertyType == typeof (uint))
							prop.SetValue (r, (uint)d, null);
						else if (prop.PropertyType == typeof (int))
							prop.SetValue (r, (int)d, null);
						else if (prop.PropertyType == typeof (double))
							prop.SetValue (r, (double)d, null);
						else if (prop.PropertyType == typeof (float))
							prop.SetValue (r, (float)d, null);
						break;
					case JsonValueType.String:
						if (prop.PropertyType == typeof (DateTime))
							prop.SetValue (r, DateTime.ParseExact ((v as JsonString).Value, DateTimeFormat, InvariantCulture), null);
						else if (prop.PropertyType.IsEnum)
							prop.SetValue (r, Enum.Parse (prop.PropertyType, (v as JsonString).Value, true), null);
						else
							prop.SetValue (r, (v as JsonString).Value, null);
						break;
					case JsonValueType.Boolean:
						prop.SetValue (r, (v as JsonBoolean).Value, null);
						break;
					case JsonValueType.Object:
						prop.SetValue (r, Deserialize (v as JsonObject, prop.PropertyType), null);
						break;
					default:
						throw new NotSupportedException ();
				}
			}

			return r;
		}

		class CacheEntry
		{
			PropertyInfo[] _properties;
			JsonObjectMappingAttribute[] _attributes;

			public CacheEntry (Type t)
			{
				PropertyInfo[] properties = t.GetProperties ();

				List<PropertyInfo> list1 = new List<PropertyInfo> (properties.Length);
				List<JsonObjectMappingAttribute> list2 = new List<JsonObjectMappingAttribute> (properties.Length);

				for (int i = 0; i < properties.Length; i ++) {
					object[] atts = properties[i].GetCustomAttributes (MappingAttributeType, true);
					if (atts.Length != 1) continue;

					list1.Add (properties[i]);
					list2.Add ((JsonObjectMappingAttribute)atts[0]);
				}

				_properties = list1.ToArray ();
				_attributes = list2.ToArray ();
			}

			public PropertyInfo[] Properties {
				get { return _properties; }
			}

			public JsonObjectMappingAttribute[] Attributes {
				get { return _attributes; }
			}
		}
	}
}
