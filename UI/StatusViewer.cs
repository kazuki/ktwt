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
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using System.Windows.Threading;
using ktwt.StatusStream;

namespace ktwt.ui
{
	class StatusViewer : UIElement, IStatusViewer, IStatusRendererOwner
	{
		List<IStatusStream> _streams = new List<IStatusStream> ();
		List<IDecoratedStatus> _statuses = new List<IDecoratedStatus> ();
		HashSet<string> _statusIDs = new HashSet<string> ();
		HashSet<string> _viewStatuses = new HashSet<string> ();
		long _lastUpdate = -1, _lastRender = -1;
		double _totalHeight = 0.0, _totalHeightCount = 0.0;
		const double MaxRenderingRate = 10;

		public StatusViewer ()
		{
			this.TextFormatter = TextFormatter.Create (TextFormattingMode.Ideal);
			this.ImageCache = new ImageCache ("image_cache");

			Focusable = true;
			ThreadSafeInvalidateVisualDelegateInstance = new EmptyDelegate (ThreadSafeInvalidateVisual);
			DispatcherTimer timer = new DispatcherTimer (TimeSpan.FromSeconds (1.0 / MaxRenderingRate), DispatcherPriority.Render, FireRenderTimer, Dispatcher);
			timer.Start ();
		}

		void FireRenderTimer (object sender, EventArgs e)
		{
			if (_lastUpdate > _lastRender) {
				bool autoScroll = (VerticalScrollBarValue == VerticalScrollBarMin);
				bool rendering = true;
				if (VerticalScrollBarMin != -(_statuses.Count - 1)) {
					VerticalScrollBarMin = -(_statuses.Count - 1);
					if (autoScroll) {
						VerticalScrollBarValue = VerticalScrollBarMin;
					} else {
						rendering = false;
					}
				}
				if (_totalHeightCount > 0)
					AverageViewPortSize = _totalHeight / _totalHeightCount;
				if (rendering)
					InvalidateVisual ();
			}
		}

		void AddStatuses (IDecoratedStatus[] items)
		{
			lock (_statusIDs) {
				for (int i = 0; i < items.Length; i ++) {
					if (!_statusIDs.Add (items[i].Status.ID))
						items[i] = null;
				}
			}

			lock (_statuses) {
				for (int i = 0; i < items.Length; i ++) {
					IDecoratedStatus s = items[i];
					int pos = _statuses.Count - 1;
					for (; pos >= 0; pos --) {
						if (_statuses[pos].Status.CreatedAt <= s.Status.CreatedAt)
							break;
					}
					_statuses.Insert (pos + 1, s);
				}
			}

			EnqueueInvalidateVisual ();
		}

		public void EnqueueInvalidateVisual ()
		{
			_lastUpdate = DateTime.Now.Ticks;
#if DEBUG
			Console.WriteLine ("EnqueueInvalidateVisual {0} {1}", DateTime.Now, DateTime.Now.Ticks);
#endif
		}

		protected override void OnMouseWheel (MouseWheelEventArgs e)
		{
			int newValue = VerticalScrollBarValue - Math.Sign (e.Delta);
			if (newValue < VerticalScrollBarMin)
				newValue = VerticalScrollBarMin;
			else if (newValue > VerticalScrollBarMax)
				newValue = VerticalScrollBarMax;
			if (newValue != VerticalScrollBarValue)
				VerticalScrollBarValue = newValue;
		}

		delegate void EmptyDelegate ();
		EmptyDelegate ThreadSafeInvalidateVisualDelegateInstance;
		public void ThreadSafeInvalidateVisual ()
		{
			if (Dispatcher.Thread != Thread.CurrentThread) {
				Dispatcher.Invoke (ThreadSafeInvalidateVisualDelegateInstance);
				return;
			}
			InvalidateVisual ();
		}
		
		protected override void OnRender (DrawingContext drawingContext)
		{
			double y = 0.0;
			Size size = RenderSize;
			_lastRender = DateTime.Now.Ticks;

#if DEBUG
			Console.WriteLine ("Rendering {0} {1}", DateTime.Now, DateTime.Now.Ticks);
#endif
			// mouse wheel handling
			drawingContext.DrawRectangle (Brushes.White, null, new Rect (0, 0, size.Width, size.Height));

			lock (_statuses) {
				_viewStatuses.Clear ();
				if (_statuses.Count > 0) {
					for (int i = VerticalScrollBarValue; i <= 0; i ++) {
						IStatusRenderer renderer =  _statuses[-i].Renderer;
						double h = renderer.Render (this, drawingContext, _statuses[-i], y, size.Width);
						_viewStatuses.Add (_statuses[-i].Status.ID);
						y += h;
						_totalHeight += h;
						_totalHeightCount += 1.0;
						if (y >= size.Height)
							break;
					}
				}
			}
		}

		#region Properties
		public static readonly DependencyProperty VerticalScrollBarValueProperty =
			DependencyProperty.Register ("VerticalScrollBarValue", typeof (int), typeof (StatusViewer), new FrameworkPropertyMetadata (0, FrameworkPropertyMetadataOptions.AffectsRender, PropertyChanged));
		public int VerticalScrollBarValue {
			get { return (int)GetValue (VerticalScrollBarValueProperty); }
			set { SetValue (VerticalScrollBarValueProperty, value); }
		}

		public static readonly DependencyProperty VerticalScrollBarMinProperty =
			DependencyProperty.Register ("VerticalScrollBarMin", typeof (int), typeof (StatusViewer), new FrameworkPropertyMetadata (0, FrameworkPropertyMetadataOptions.AffectsRender, null));
		public int VerticalScrollBarMin {
			get { return (int)GetValue (VerticalScrollBarMinProperty); }
			set { SetValue (VerticalScrollBarMinProperty, value); }
		}

		public static readonly DependencyProperty VerticalScrollBarMaxProperty =
			DependencyProperty.Register ("VerticalScrollBarMax", typeof (int), typeof (StatusViewer), new FrameworkPropertyMetadata (0, FrameworkPropertyMetadataOptions.AffectsRender, null));
		public int VerticalScrollBarMax {
			get { return (int)GetValue (VerticalScrollBarMaxProperty); }
			set { SetValue (VerticalScrollBarMaxProperty, value); }
		}

		public static readonly DependencyProperty AverageViewPortSizeProperty =
			DependencyProperty.Register ("AverageViewPortSize", typeof (double), typeof (StatusViewer), new FrameworkPropertyMetadata (1.0, FrameworkPropertyMetadataOptions.None, null));
		public double AverageViewPortSize {
			get { return (double)GetValue (AverageViewPortSizeProperty); }
			private set { SetValue (AverageViewPortSizeProperty, value); }
		}

		static void PropertyChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((UIElement)d).InvalidateVisual ();
		}

		public TextFormatter TextFormatter { get; private set; }
		public ImageCache ImageCache { get; private set; }
		#endregion

		#region IStatusViewer
		public void AddInputStream (IStatusStream strm)
		{
			lock (_streams) {
				if (_streams.Contains (strm))
					return;
				_streams.Add (strm);
			}
			strm.StatusesArrived += StatusStream_StatusesArrived;
		}

		void StatusStream_StatusesArrived (object sender, StatusesArrivedEventArgs e)
		{
			IDecoratedStatus[] items = new IDecoratedStatus[e.Statuses.Length];
			for (int i = 0; i < e.Statuses.Length; i ++) {
				IStatusRenderer renderer = StatusRenderers.GetRenderer (e.Statuses[i].GetType ());
				items[i] = renderer.Decorate (e.Statuses[i]);
			}
			AddStatuses (items);
		}

		public void RemoveInputStream (IStatusStream strm)
		{
			strm.StatusesArrived -= StatusStream_StatusesArrived;
			lock (_streams) {
				_streams.Remove (strm);
			}
		}

		public IStatusStream[] InputStreams {
			get { return _streams.ToArray (); }
		}

		public string Name {
			get { return "viewer"; }
		}
		#endregion
	}
}
