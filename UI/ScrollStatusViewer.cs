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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ktwt.StatusStream;

namespace ktwt.ui
{
	class ScrollStatusViewer : Grid, IStatusViewer
	{
		public ScrollStatusViewer ()
		{
			this.ColumnDefinitions.Add (new ColumnDefinition ());
			this.ColumnDefinitions.Add (new ColumnDefinition {Width = GridLength.Auto});
			this.RowDefinitions.Add (new RowDefinition ());

			StatusViewer = new StatusViewer ();
			StatusViewer.SetValue (Grid.ColumnProperty, 0);
			StatusViewer.SetValue (Grid.RowProperty, 0);

			VerticalScrollBar = new ScrollBar {Orientation = Orientation.Vertical, SmallChange = 1.0, LargeChange = 20.0};
			BindingOperations.SetBinding (VerticalScrollBar, ScrollBar.ViewportSizeProperty, new Binding {Source = StatusViewer, Path = new PropertyPath (StatusViewer.AverageViewPortSizeProperty), Mode = BindingMode.OneWay});
			BindingOperations.SetBinding (VerticalScrollBar, ScrollBar.ValueProperty, new Binding {Source = StatusViewer, Path = new PropertyPath (StatusViewer.VerticalScrollBarValueProperty)});
			BindingOperations.SetBinding (VerticalScrollBar, ScrollBar.MinimumProperty, new Binding {Source = StatusViewer, Path = new PropertyPath (StatusViewer.VerticalScrollBarMinProperty)});
			BindingOperations.SetBinding (VerticalScrollBar, ScrollBar.MaximumProperty, new Binding {Source = StatusViewer, Path = new PropertyPath (StatusViewer.VerticalScrollBarMaxProperty)});
			VerticalScrollBar.SetValue (Grid.ColumnProperty, 1);
			VerticalScrollBar.SetValue (Grid.RowProperty, 0);

			Children.Add (StatusViewer);
			Children.Add (VerticalScrollBar);
		}

		public StatusViewer StatusViewer { get; private set; }
		public ScrollBar VerticalScrollBar { get; private set; }

		public void AddInputStream (IStatusStream strm)
		{
			StatusViewer.AddInputStream (strm);
		}

		public void RemoveInputStream (IStatusStream strm)
		{
			StatusViewer.RemoveInputStream (strm);
		}

		public IStatusStream[] InputStreams {
			get { return StatusViewer.InputStreams; }
		}
	}
}
