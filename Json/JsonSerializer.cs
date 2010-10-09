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
using System.IO;
using System.Reflection;
using System.Text;

namespace ktwt.Json
{
	public static class JsonSerializer
	{
		public static string Serialize (object obj)
		{
			StringBuilder sb = new StringBuilder ();
			using (StringWriter writer = new StringWriter (sb)) {
				Serialize (writer, obj);
			}
			return sb.ToString ();
		}

		public static void Serialize (TextWriter writer, object obj)
		{
			if (obj == null)
				return;

			using (JsonTextWriter jtw = new JsonTextWriter (writer)) {
				if (obj is Array)
					Serialize (jtw, (Array)obj);
				else
					Serialize (jtw, obj);
			}
		}

		static void Serialize (JsonTextWriter writer, object obj)
		{
			writer.WriteStartObject ();

			Type type = obj.GetType ();
			SerializationCache c = SerializationCache.Get (type);
			for (int i = 0; i < c.Attributes.Length; i ++) {
				PropertyInfo p = c.Properties[i];
				JsonObjectMappingAttribute a = c.Attributes[i];

				object v = p.GetValue (obj, null);
				if (v == null)
					continue;
				writer.WriteKey (a.Key);
				Serialize (writer, a.ValueType, v);
			}

			writer.WriteEndObject ();
		}

		static void Serialize (JsonTextWriter writer, Array ary)
		{
			JsonValueType type;
			Type t = ary.GetType().GetElementType();
			if (t.Equals (typeof (string))) {
				type = JsonValueType.String;
			} else if (t.IsArray) {
				type = JsonValueType.Array;
			} else if (t.IsClass) {
				type = JsonValueType.Object;
			} else if (t.IsPrimitive) {
				if (t.Equals (typeof (bool)))
					type = JsonValueType.Boolean;
				if (t.Equals (typeof (IntPtr)) || t.Equals (typeof (UIntPtr)) || t.Equals (typeof (Char)))
					throw new NotSupportedException ();
				type = JsonValueType.Number;
			} else {
				throw new NotSupportedException ();
			}

			writer.WriteStartArray ();
			for (int i = 0; i < ary.Length; i ++) {
				Serialize (writer, type, ary.GetValue (i));
			}
			writer.WriteEndArray ();
		}

		static void Serialize (JsonTextWriter writer, JsonValueType jvType, object obj)
		{
			switch (jvType) {
				case JsonValueType.Array:
					Serialize (writer, (Array)obj);
					break;
				case JsonValueType.Boolean:
					writer.WriteBoolean ((bool)obj);
					break;
				case JsonValueType.Null:
					writer.WriteNull ();
					break;
				case JsonValueType.Number:
					Type t = obj.GetType ();
					if (t == typeof (ulong))
						writer.WriteNumber ((ulong)obj);
					else if (t == typeof (long))
						writer.WriteNumber ((long)obj);
					else if (t == typeof (uint))
						writer.WriteNumber ((ulong)(uint)obj);
					else if (t == typeof (int))
						writer.WriteNumber ((long)(int)obj);
					else if (t == typeof (double))
						writer.WriteNumber ((double)obj);
					else if (t == typeof (float))
						writer.WriteNumber ((double)(float)obj);
					else if (t == typeof (ushort))
						writer.WriteNumber ((ulong)(ushort)obj);
					else if (t == typeof (short))
						writer.WriteNumber ((long)(short)obj);
					else if (t == typeof (byte))
						writer.WriteNumber ((ulong)(byte)obj);
					else if (t == typeof (sbyte))
						writer.WriteNumber ((long)(sbyte)obj);
					else
						throw new ArgumentException ();
					break;
				case JsonValueType.Object:
					Serialize (writer, obj);
					break;
				case JsonValueType.String:
					writer.WriteString (obj.ToString ());
					break;
				default:
					throw new ArgumentException ();
			}
		}
	}
}
