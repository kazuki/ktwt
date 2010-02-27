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
using System.Windows.Media;
using System.Windows.Shapes;

namespace TwitterStreaming
{
	public class StarShape : Shape
	{
		static Dictionary<CacheKey, PathGeometry> cache = new Dictionary<CacheKey, PathGeometry> ();
		static PathGeometry EmptyGeometry = new PathGeometry ();
		static CacheKey EmptyKey = new CacheKey (0.0, 0.0);
		PathGeometry _path = EmptyGeometry;
		CacheKey _key = EmptyKey;

		static StarShape ()
		{
			cache.Add (EmptyKey, EmptyGeometry);
		}

		public StarShape ()
		{
		}

		void UpdateGeometry (CacheKey key)
		{
			int apexs = 5;
			double OuterRadius = key.OuterRadius;
			double InnerRadius = key.OuterRadius / 2.0;

			if (cache.TryGetValue (key, out _path)) {
				_key = key;
				return;
			}
			PathFigure figure = new PathFigure ();
			PolyLineSegment segments = new PolyLineSegment ();
			double add = 2 * Math.PI / (apexs * 2);
			double angle = add;
			RotateTransform trans = new RotateTransform (-90.0, OuterRadius, 0.0);
			TranslateTransform trans2 = new TranslateTransform (key.X, key.Y);
			Point[] points = new Point[apexs * 2];

			figure.StartPoint = trans2.Transform (trans.Transform (new Point (OuterRadius, 0)));
			for (int i = 0; i < points.Length - 1; i++, angle += add) {
				Point p = (i % 2 == 0 ? new Point (InnerRadius * Math.Cos (angle), InnerRadius * Math.Sin (angle))
					: new Point (OuterRadius * Math.Cos (angle), OuterRadius * Math.Sin (angle)));
				segments.Points.Add (trans2.Transform (trans.Transform (p)));
			}
			figure.Segments.Add (segments);
			figure.IsClosed = true;
			_path = new PathGeometry (new PathFigure[] {figure});
			segments.Freeze ();
			figure.Freeze ();
			_path.Freeze ();
			_key = key;
			cache.Add (key, _path);
		}

		protected override Geometry DefiningGeometry
		{
			get {
				CacheKey key = new CacheKey (ActualWidth, ActualHeight);
				if (_path == null || !_key.Equals (key))
					UpdateGeometry (key);
				return _path;
			}
		}

		class CacheKey : IEquatable<CacheKey>
		{
			double _outer, _x, _y;

			public CacheKey (double width, double height)
			{
				_outer = Math.Min (width, height) / 2.0;
				_x = width / 2.0 - _outer;
				_y = height / 2.0 - _outer;
			}

			public double OuterRadius {
				get { return _outer; }
			}

			public double X {
				get { return _x; }
			}

			public double Y {
				get { return _y; }
			}

			public override int GetHashCode ()
			{
				return _outer.GetHashCode () ^ _x.GetHashCode () ^ _y.GetHashCode ();
			}

			public override bool Equals (object obj)
			{
				return Equals ((CacheKey)obj);
			}

			public bool Equals (CacheKey other)
			{
				return _outer == other._outer && _x == other._x && _y == other._y;
			}
		}
	}
}
