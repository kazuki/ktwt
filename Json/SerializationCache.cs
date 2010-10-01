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

namespace ktwt.Json
{
	class SerializationCache
	{
		static readonly Type MappingAttributeType = typeof (JsonObjectMappingAttribute);
		static Dictionary<Type, SerializationCache> _reflectionCache = new Dictionary<Type, SerializationCache> ();
		public const string JsonDateTimeFormat = "ddd MMM dd HH:mm:ss zzzz yyyy";
		public static IFormatProvider InvariantCulture = CultureInfo.InvariantCulture;
		
		PropertyInfo[] _properties;
		JsonObjectMappingAttribute[] _attributes;

		public static SerializationCache Get (Type t)
		{
			SerializationCache c;
			lock (_reflectionCache) {
				if (!_reflectionCache.TryGetValue (t, out c)) {
					c = new SerializationCache (t);
					_reflectionCache.Add (t, c);
				}
			}
			return c;
		}

		SerializationCache (Type t)
		{
			PropertyInfo[] properties = t.GetProperties (BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetField | BindingFlags.SetProperty);

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
