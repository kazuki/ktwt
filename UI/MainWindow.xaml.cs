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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ktwt.Threading;
using ktwt.Twitter;

namespace ktwt.ui
{
	partial class MainWindow : Window
	{
		IntervalTimer _timer;
		TwitterAccountNode[] _nodes;
		Configurations _config;
		Dictionary<string, ScrollStatusViewer> _viewers = new Dictionary<string,ScrollStatusViewer> ();

		public MainWindow (Configurations config)
		{
			InitializeComponent ();

			_config = config;
			_timer = new IntervalTimer (TimeSpan.FromSeconds (0.5), Environment.ProcessorCount);
			_nodes = new TwitterAccountNode[_config.TwitterAccounts.Length];
			for (int i = 0; i < _config.TwitterAccounts.Length; i ++) {
				_nodes[i] = new TwitterAccountNode (_timer);
				_nodes[i].Credential = _config.TwitterAccounts[i];
			}

			UIElement mainPane = CreatePane (config.PaneInfo);
			mainPane.SetValue (Grid.ColumnProperty, 0);
			mainPane.SetValue (Grid.RowProperty, 0);
			mainGrid.Children.Add (mainPane);
		}

		UIElement CreatePane (Configurations.PaneConfig config)
		{
			Configurations.SplitterPaneInfo pi = config.SplitterConfig;
			Configurations.SplitterLayoutInfo pli = config.SplitterLayoutConfig;
			UIElement pane = null;

			switch (config.Type) {
				case Configurations.PaneType.Splitter: {
					Grid grid = new Grid ();
					if (pi != null) {
						for (int i = 0; i < pi.Rows; i ++)
							grid.RowDefinitions.Add (new RowDefinition ());
						for (int i = 0; i < pi.Columns; i ++)
							grid.ColumnDefinitions.Add (new ColumnDefinition ());
					}
					pane = grid;
					break;
				}
				case Configurations.PaneType.Viewer: {
					Grid grid = new Grid ();
					if (pli != null) {
						Grid.SetRow (grid, pli.Row);
						Grid.SetRowSpan (grid, pli.RowSpan);
						Grid.SetColumn (grid, pli.Column);
						Grid.SetColumnSpan (grid, pli.ColumnSpan);
					}
					grid.RowDefinitions.Add (new RowDefinition {Height=GridLength.Auto});
					grid.RowDefinitions.Add (new RowDefinition ());
					grid.ColumnDefinitions.Add (new ColumnDefinition ());

					TextBlock tb = new TextBlock ();
					tb.Text = config.Caption;
					tb.Padding = new Thickness (3, 2, 0, 2);
					grid.Children.Add (tb);

					ScrollStatusViewer viewer = new ScrollStatusViewer ();
					grid.Children.Add (viewer);
					Grid.SetRow (viewer, 1);
					Grid.SetColumn (viewer, 0);

					_viewers[config.Id] = viewer;
					pane = grid;
					break;
				}
				case Configurations.PaneType.Tab:
					throw new NotImplementedException ();
				default:
					throw new FormatException ();
			}

			IAddChild ac = pane as IAddChild;
			if (ac != null && config.Children != null) {
				for (int i = 0; i < config.Children.Length; i ++)
					ac.AddChild (CreatePane (config.Children[i]));
			}
			return pane;
		}

		protected override void OnClosed (EventArgs e)
		{
			_timer.Dispose ();
			for (int i = 0; i < _nodes.Length; i ++)
				_nodes[i].Dispose ();
			base.OnClosed (e);
		}
	}
}
