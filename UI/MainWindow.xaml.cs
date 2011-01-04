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
using ktwt.StatusStream;
using ktwt.Twitter;
using ktwt.Twitter.ui;

namespace ktwt.ui
{
	partial class MainWindow : Window
	{
		IntervalTimer _timer;
		Configurations _config;
		Dictionary<FilterGraphNodeKey, INamedElement> _nodes = new Dictionary<FilterGraphNodeKey, INamedElement> ();
		Dictionary<string, ScrollStatusViewer> _viewers = new Dictionary<string,ScrollStatusViewer> ();

		public MainWindow (Configurations config)
		{
			InitializeComponent ();
			if (config.Window != null) {
				Left = (config.Window.X >= 0 ? config.Window.X : Left);
				Top = (config.Window.Y >= 0 ? config.Window.Y : Top);
				Width = (config.Window.Width >= 0 ? config.Window.Width : Width);
				Height = (config.Window.Height >= 0 ? config.Window.Height : Height);
				WindowState = config.Window.State;
			}

			OptionWindow optWin = new OptionWindow (config);
			config = optWin.Config;
			if (optWin.ShowDialog () != true || config.Accounts.Length == 0) {
				Application.Current.Shutdown ();
				return;
			}

			if (config.Accounts != null) {
				for (int i = 0; i < config.Accounts.Length; i ++)
					_nodes.Add (new FilterGraphNodeKey (ElementType.Account, config.Accounts[i].ID), config.Accounts[i]);
			}

			if (config.PaneInfo == null) {
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
			}

			_config = config;
			_timer = new IntervalTimer (TimeSpan.FromSeconds (0.5), Environment.ProcessorCount);

			UIElement mainPane = CreatePane (config.PaneInfo);
			mainPane.SetValue (Grid.ColumnProperty, 0);
			mainPane.SetValue (Grid.RowProperty, 0);
			mainGrid.Children.Add (mainPane);
			FilterGraph.Construct (config.Edges, _nodes);

			for (int i = 0; i < _config.Accounts.Length; i ++)
				_config.Accounts[i].Start (_timer);
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
					_nodes.Add (new FilterGraphNodeKey (ElementType.Viewer, config.Id), viewer);
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
			if (_timer == null)
				return;

			_timer.Dispose ();
			for (int i = 0; i < _config.Accounts.Length; i ++)
				_config.Accounts[i].Dispose ();
			base.OnClosed (e);

			if (WindowState == WindowState.Normal) {
				_config.Window = new Configurations.WindowInfo {
					X = (int)Left, Y = (int)Top, Width = (int)Width, Height = (int)Height, State = System.Windows.WindowState.Normal
				};
			} else if (WindowState == WindowState.Maximized) {
				if (_config.Window == null) {
					_config.Window = new Configurations.WindowInfo {
						State = WindowState.Maximized
					};
				} else {
					_config.Window.State = WindowState.Maximized;
				}
			}
			_config.Save ();
		}
	}
}
