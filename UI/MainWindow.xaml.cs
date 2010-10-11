﻿/*
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

			OptionWindow optWin = new OptionWindow (config);
			config = optWin.Config;
			if (optWin.ShowDialog () != true || config.TwitterAccounts.Length == 0) {
				Application.Current.Shutdown ();
				return;
			}

			// test pane layouy & stream assign
			config.PaneInfo = new Configurations.PaneConfig {
				Type = Configurations.PaneType.Splitter,
				SplitterConfig = new Configurations.SplitterPaneInfo { Columns = 2, Rows = 2 },
				Children = new Configurations.PaneConfig[] {
					new Configurations.PaneConfig {
						Type = Configurations.PaneType.Viewer,
						Caption = "home",
						Id = "home",
						SplitterLayoutConfig = new Configurations.SplitterLayoutInfo {Column = 0, ColumnSpan = 1, Row = 0, RowSpan = 2}
					},
					new Configurations.PaneConfig {
						Type = Configurations.PaneType.Viewer,
						Caption = "mentions",
						Id = "mentions",
						SplitterLayoutConfig = new Configurations.SplitterLayoutInfo {Column = 1, ColumnSpan = 1, Row = 0, RowSpan = 1}
					},
					new Configurations.PaneConfig {
						Type = Configurations.PaneType.Viewer,
						Caption = "dm",
						Id = "dm",
						SplitterLayoutConfig = new Configurations.SplitterLayoutInfo {Column = 1, ColumnSpan = 1, Row = 1, RowSpan = 1}
					},
				}
			};
			config.Save ();

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

			for (int i = 0; i < _config.TwitterAccounts.Length; i ++) {
				_viewers["home"].AddInputStream (_nodes[i].AddUserStream ());
				_viewers["home"].AddInputStream (_nodes[i].AddRestStream (new RestUsage {Type=RestType.Home, Count=300, Interval=TimeSpan.FromSeconds (60), IsEnabled=true}));
				_viewers["mentions"].AddInputStream (_nodes[i].AddRestStream (new RestUsage {Type=RestType.Mentions, Count=300, Interval=TimeSpan.FromSeconds (120), IsEnabled=true}));
				_viewers["dm"].AddInputStream (_nodes[i].AddRestStream (new RestUsage {Type=RestType.DirectMessages, Count=300, Interval=TimeSpan.FromSeconds (600), IsEnabled=true}));
			}
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
