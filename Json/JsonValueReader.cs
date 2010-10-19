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
using System.IO;
using LitJson;

namespace ktwt.Json
{
	public class JsonValueReader
	{
		JsonReader _reader;

		public JsonValueReader (string text)
			: this (new JsonReader (text))
		{
		}

		public JsonValueReader (TextReader reader)
			: this (new JsonReader (reader))
		{
		}

		public JsonValueReader (JsonReader reader)
		{
			_reader = reader;
		}

		public static JsonValue Read (string text)
		{
			return Read (new JsonReader (text));
		}

		public static JsonValue Read (TextReader reader)
		{
			return Read (new JsonReader (reader));
		}

		public static JsonValue Read (JsonReader reader)
		{
			return new JsonValueReader (reader).Read ();
		}

		public JsonValue Read ()
		{
			JsonValue cur = null;
			string lastPropName = null;
			Stack<JsonValue> stack = new Stack<JsonValue> ();
			Stack<string> propNameStack = new Stack<string> ();
			JsonReader reader = _reader;

			while (reader.Read ()) {
				switch (reader.Token) {
					case JsonToken.ArrayStart:
					case JsonToken.ObjectStart:
						if (cur != null) {
							stack.Push (cur);
							if (cur is JsonObject) {
								propNameStack.Push (lastPropName);
								lastPropName = null;
							}
						}
						if (reader.Token == JsonToken.ArrayStart)
							cur = new JsonArray (new List<JsonValue> ());
						else
							cur = new JsonObject (new Dictionary<string,JsonValue> ());
						break;
					case JsonToken.ObjectEnd:
					case JsonToken.ArrayEnd:
						if (stack.Count == 0)
							return cur;
						JsonValue parent = stack.Pop ();
						if (parent is JsonArray) {
							(parent as JsonArray).Value.Add (cur);
						} else if (parent is JsonObject) {
							lastPropName = propNameStack.Pop ();
							if (lastPropName == null)
								throw new JsonException ();
							(parent as JsonObject).Value.Add (lastPropName, cur);
							lastPropName = null;
						}
						cur = parent;
						break;
					case JsonToken.PropertyName:
						if (lastPropName != null)
							throw new JsonException ();
						lastPropName = (string)reader.Value;
						break;
					case JsonToken.Boolean:
					case JsonToken.Null:
					case JsonToken.Number:
					case JsonToken.String:
						JsonValue value;
						switch (reader.Token) {
							case JsonToken.Boolean: value = new JsonBoolean ((bool)reader.Value); break;
							case JsonToken.Null: value = new JsonNull (); break;
							case JsonToken.Number:
								value = new JsonNumber (Convert (reader.NumberType), (double)reader.Value, reader.ValueSignedInteger, reader.ValueUnsignedInteger);
								break;
							case JsonToken.String: value = new JsonString ((string)reader.Value); break;
							default: throw new JsonException ();
						}
						if (cur == null)
							return value;
						if (cur is JsonArray) {
							(cur as JsonArray).Value.Add (value);
						} else if (cur is JsonObject) {
							if (lastPropName == null)
								throw new JsonException ();
							(cur as JsonObject).Value.Add (lastPropName, value);
							lastPropName = null;
						}
						break;
				}
			}

			if (cur == null)
				return null;
			throw new JsonException ();
		}

		static JsonNumberType Convert (LitJson.JsonNumberType type)
		{
			if (type == LitJson.JsonNumberType.SignedInteger)
				return JsonNumberType.Signed;
			if (type == LitJson.JsonNumberType.UnsignedInteger)
				return JsonNumberType.Unsigned;
			return JsonNumberType.FloatingPoint;
		}
	}
}
