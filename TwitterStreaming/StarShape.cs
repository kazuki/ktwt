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
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TwitterStreaming
{
	public class StarShape : Shape
	{
		public StarShape ()
		{
		}

		protected override Geometry DefiningGeometry
		{
			get
			{
				int apexs = 5;
				double OuterRadius = Math.Min (this.ActualHeight, this.ActualWidth) / 2.0;
				double InnerRadius = OuterRadius / 2.0;
				double offset_y = this.ActualHeight / 2.0 - OuterRadius;
				double offset_x = this.ActualWidth / 2.0 - OuterRadius;

				PathFigure figure = new PathFigure ();
				PolyLineSegment segments = new PolyLineSegment ();
				double add = 2 * Math.PI / (apexs * 2);
				double angle = add;
				RotateTransform trans = new RotateTransform (-90.0, OuterRadius, 0.0);
				TranslateTransform trans2 = new TranslateTransform (offset_x, offset_y);
				Point[] points = new Point[apexs * 2];

				figure.StartPoint = trans2.Transform (trans.Transform (new Point (OuterRadius, 0)));
				for (int i = 0; i < points.Length - 1; i++, angle += add) {
					Point p = (i % 2 == 0 ? new Point (InnerRadius * Math.Cos (angle), InnerRadius * Math.Sin (angle))
						: new Point (OuterRadius * Math.Cos (angle), OuterRadius * Math.Sin (angle)));
					segments.Points.Add (trans2.Transform (trans.Transform (p)));
				}
				figure.Segments.Add (segments);
				figure.IsClosed = true;
				return new PathGeometry (new PathFigure[] { figure });
			}
		}
	}
}
