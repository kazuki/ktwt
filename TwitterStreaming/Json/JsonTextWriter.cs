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
using System.IO;
using System.Text;

namespace ktwt.Json
{
	public class JsonTextWriter : IDisposable
	{
		TextWriter _writer;
		State _current = null;
		Stack<State> _stack = new Stack<State> ();

		public JsonTextWriter (TextWriter writer)
		{
			_writer = writer;
		}

		public void WriteStartObject ()
		{
			StartBracket ('{');
			_current = new State (StateType.Object);
		}

		public void WriteEndObject ()
		{
			if (_current == null || _current.StateType != StateType.Object)
				throw new Exception ();
			if (_current.IsInValueArea)
				WriteNull ();
			EndBracket ('}');
		}

		public void WriteStartArray ()
		{
			StartBracket ('[');
			_current = new State (StateType.Array);
		}

		public void WriteEndArray ()
		{
			if (_current == null || _current.StateType != StateType.Array)
				throw new Exception ();
			EndBracket (']');
		}

		void StartBracket (char c)
		{
			if (_current != null) {
				if (_current.StateType == StateType.Object && !_current.IsInValueArea)
					throw new Exception ();
				BeforeWriteValue ();
			}
			_stack.Push (_current);
			_writer.Write (c);
		}

		void EndBracket (char c)
		{
			_writer.Write (c);
			_current = _stack.Pop ();
			if (_current != null && _current.StateType == StateType.Object)
				_current.IsInValueArea = false;
		}

		void BeforeWriteValue ()
		{
			if (_current == null)
				return;
			if (_current.StateType == StateType.Array && _current.NumberOfValues++ != 0)
				_writer.Write (',');
		}

		void WriteValue (string text)
		{
			BeforeWriteValue ();
			_writer.Write (text);
			if (_current != null && _current.StateType == StateType.Object)
				_current.IsInValueArea = false;
		}

		string EscapeString (string text)
		{
			StringBuilder sb = new StringBuilder ();
			sb.Append ('\"');
			for (int i = 0; i < text.Length; i++) {
				switch (text[i]) {
					case '\"': sb.Append ("\\\""); break;
					case '\\': sb.Append ("\\\\"); break;
					case '/': sb.Append ("\\/"); break;
					case '\n': sb.Append ("\\n"); break;
					case '\r': sb.Append ("\\r"); break;
					case '\t': sb.Append ("\\t"); break;
					default: sb.Append (text[i]); break;
				}
			}
			sb.Append ('\"');
			return sb.ToString ();
		}

		public void WriteKey (string text)
		{
			if (_current == null || _current.StateType != StateType.Object)
				throw new Exception ();
			if (_current.IsInValueArea)
				throw new Exception ();
			if (_current.NumberOfKeys++ != 0)
				_writer.Write (',');
			_writer.Write (EscapeString (text));
			_writer.Write (':');
			_current.IsInValueArea = true;
		}

		public void WriteNull ()
		{
			WriteValue ("null");
		}

		public void WriteString (string text)
		{
			WriteValue (EscapeString (text));
		}

		public void WriteBoolean (bool value)
		{
			WriteValue (value ? "true" : "false");
		}

		public void WriteNumber (long value)
		{
			WriteValue (value.ToString ());
		}

		public void WriteNumber (double value)
		{
			WriteValue (value.ToString ());
		}

		public void Close ()
		{
			while (_current != null) {
				if (_current.StateType == StateType.Array)
					WriteEndArray ();
				else if (_current.StateType == StateType.Object)
					WriteEndObject ();
			}
		}

		public void Dispose ()
		{
			Close ();
		}

		class State
		{
			public State (StateType type)
			{
				StateType = type;
				NumberOfValues = 0;
				NumberOfKeys = 0;
				IsInValueArea = false;
			}
			public StateType StateType { get; private set; }
			public int NumberOfKeys { get; set; }
			public int NumberOfValues { get; set; }
			public bool IsInValueArea { get; set; }
		}

		enum StateType
		{
			Object,
			Array
		}
	}
}
