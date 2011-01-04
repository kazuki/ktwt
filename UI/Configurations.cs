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
using System.Text;
using System.Windows;
using ktwt.Json;

namespace ktwt.ui
{
	public class Configurations
	{
		public static Configurations Load (string path)
		{
			try {
				if (File.Exists (path)) {
					using (StreamReader reader = new StreamReader (path)) {
						Configurations config = JsonDeserializer.Deserialize<Configurations> (reader.ReadToEnd ());
						config.ConfigurationFilePath = path;
						return config;
					}
				}
			} catch {}
			return new Configurations { ConfigurationFilePath = path };
		}

		Configurations ()
		{
			Accounts = new IAccountInfo[0];
			Edges = new FilterGraphEdgeKey[0];
		}

		public void Save ()
		{
			using (StreamWriter writer = new StreamWriter (ConfigurationFilePath)) {
				JsonSerializer.Serialize (writer, this);
			}
		}

		public Configurations Clone ()
		{
			StringBuilder sb = new StringBuilder ();
			using (StringWriter writer = new StringWriter (sb)) {
				JsonSerializer.Serialize (writer, this);
			}
			Configurations config = JsonDeserializer.Deserialize<Configurations> (sb.ToString ());
			config.ConfigurationFilePath = this.ConfigurationFilePath;
			return config;
		}

		public string ConfigurationFilePath { get; set; }

		Dictionary<string, string>[] _accounts_internal;
		[JsonObjectMapping ("accounts", JsonValueType.Array)]
		Dictionary<string, string>[] AccountsInternal {
			get { return _accounts_internal; }
			set {
				_accounts_internal = value;
				_accounts = new IAccountInfo[value.Length];
				for (int i = 0; i < value.Length; i ++)
					_accounts[i] = StatusTypes.DeserializeAccountInfo (value[i]);
			}
		}

		IAccountInfo[] _accounts;
		public IAccountInfo[] Accounts {
			get { return _accounts; }
			set {
				_accounts = value;
				_accounts_internal = new Dictionary<string,string>[value.Length];
				for (int i = 0; i < value.Length; i ++)
					_accounts_internal[i] = StatusTypes.SerializeAccountInfo (value[i]);
			}
		}

		Dictionary<string, string>[] _edges_internal;
		[JsonObjectMapping ("edges", JsonValueType.Array)]
		Dictionary<string, string>[] EdgesInternal {
			get { return _edges_internal; }
			set {
				_edges_internal = value;
				_edges = new FilterGraphEdgeKey[value.Length];
				for (int i = 0; i < value.Length; i ++)
					_edges[i] = FilterGraphEdgeKey.Deserialize (value[i]);
			}
		}

		FilterGraphEdgeKey[] _edges;
		public FilterGraphEdgeKey[] Edges {
			get { return _edges; }
			set {
				_edges = value;
				_edges_internal = new Dictionary<string,string>[value.Length];
				for (int i = 0; i < value.Length; i ++)
					_edges_internal[i] = FilterGraphEdgeKey.Serialize (value[i]);
			}
		}

		[JsonObjectMapping ("pane", JsonValueType.Object)]
		public PaneConfig PaneInfo { get; set; }

		[JsonObjectMapping ("window", JsonValueType.Object)]
		public WindowInfo Window { get; set; }

		#region Internal Classes
		/// <summary>
		/// Type毎の必須フィールド
		/// 0 = Viewer, 1 = Splitter, 2 = Tab
		/// # = Splitterの子供
		/// </summary>
		public class PaneConfig
		{
			[JsonObjectMapping ("type", JsonValueType.String)]
			public PaneType Type { get; set; }

			/// <summary>
			/// Required by "0"
			/// </summary>
			[JsonObjectMapping ("id", JsonValueType.String)]
			public string Id { get; set; }

			/// <summary>
			/// Required by "0,2"
			/// </summary>
			[JsonObjectMapping ("caption", JsonValueType.String)]
			public string Caption { get; set; }

			/// <summary>
			/// Required by "1"
			/// </summary>
			[JsonObjectMapping ("splitter", JsonValueType.Object)]
			public SplitterPaneInfo SplitterConfig { get; set; }

			/// <summary>
			/// Required by "#"
			/// </summary>
			[JsonObjectMapping ("layout_splitter", JsonValueType.Object)]
			public SplitterLayoutInfo SplitterLayoutConfig { get; set; }

			[JsonObjectMapping ("children", JsonValueType.Array)]
			public PaneConfig[] Children { get; set; }

			public PaneConfig[] GetViewers ()
			{
				List<PaneConfig> list = new List<PaneConfig> ();
				FindViewers (list);
				return list.ToArray ();
			}

			void FindViewers (List<PaneConfig> list)
			{
				if (Type == PaneType.Viewer) {
					list.Add (this);
					return;
				}
				for (int i = 0; i < Children.Length; i ++)
					Children[i].FindViewers (list);
			}
		}

		public class SplitterPaneInfo
		{
			[JsonObjectMapping ("rows", JsonValueType.Number)]
			public int Rows { get; set; }

			[JsonObjectMapping ("columns", JsonValueType.Number)]
			public int Columns { get; set; }
		}

		public class SplitterLayoutInfo
		{
			[JsonObjectMapping ("row", JsonValueType.Number)]
			public int Row { get; set; }

			[JsonObjectMapping ("row_span", JsonValueType.Number)]
			public int RowSpan { get; set; }

			[JsonObjectMapping ("column", JsonValueType.Number)]
			public int Column { get; set; }

			[JsonObjectMapping ("column_span", JsonValueType.Number)]
			public int ColumnSpan { get; set; }
		}

		public enum PaneType
		{
			Viewer,
			Splitter,
			Tab
		}

		public class WindowInfo
		{
			[JsonObjectMapping ("x", JsonValueType.Number)]
			public int X { get; set; }

			[JsonObjectMapping ("y", JsonValueType.Number)]
			public int Y { get; set; }

			[JsonObjectMapping ("w", JsonValueType.Number)]
			public int Width { get; set; }

			[JsonObjectMapping ("h", JsonValueType.Number)]
			public int Height { get; set; }

			[JsonObjectMapping ("s", JsonValueType.String)]
			public WindowState State { get; set; }
		}
		#endregion
	}
}
