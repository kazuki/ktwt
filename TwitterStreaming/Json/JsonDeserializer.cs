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
using System.Reflection;
using System.Runtime.Serialization;

namespace ktwt.Json
{
	public static class JsonDeserializer
	{
		static readonly Type MappingAttributeType = typeof (JsonObjectMappingAttribute);
		static Dictionary<Type, CacheEntry> _reflectionCache = new Dictionary<Type, CacheEntry> ();

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
				if (!obj.Value.TryGetValue (c.Attributes[i].Key, out v) || v.ValueType != c.Attributes[i].ValueType) continue;
				switch (c.Attributes[i].ValueType) {
					case JsonValueType.Number:
						double d = (v as JsonNumber).Value;
						if (c.Properties[i].PropertyType == typeof (ulong))
							c.Properties[i].SetValue (r, (ulong)d, null);
						else if (c.Properties[i].PropertyType == typeof (long))
							c.Properties[i].SetValue (r, (long)d, null);
						else if (c.Properties[i].PropertyType == typeof (uint))
							c.Properties[i].SetValue (r, (uint)d, null);
						else if (c.Properties[i].PropertyType == typeof (int))
							c.Properties[i].SetValue (r, (int)d, null);
						else if (c.Properties[i].PropertyType == typeof (double))
							c.Properties[i].SetValue (r, (double)d, null);
						else if (c.Properties[i].PropertyType == typeof (float))
							c.Properties[i].SetValue (r, (float)d, null);
						break;
					case JsonValueType.String:
						c.Properties[i].SetValue (r, (v as JsonString).Value, null);
						break;
					case JsonValueType.Boolean:
						c.Properties[i].SetValue (r, (v as JsonBoolean).Value, null);
						break;
					case JsonValueType.Object:
						c.Properties[i].SetValue (r, Deserialize (v as JsonObject, c.Properties[i].PropertyType), null);
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
