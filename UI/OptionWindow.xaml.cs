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
using System.Windows.Shapes;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using System.Linq;
using ktwt.StatusStream;

namespace ktwt.ui
{
	public partial class OptionWindow : Window
	{
		public OptionWindow (Configurations config)
		{
			Config = config.Clone ();
			InitializeComponent ();
			btnOK.SizeChanged += delegate (object sender, SizeChangedEventArgs e) {
				OverlapMargin = new Thickness (5, 5, 5, 10.0 + btnOK.ActualHeight);
			};
			if (AccountTypes.Count > 0)
				account_type.SelectedIndex = 0;
			ConstructFilterGraph ();

			filterGraphCanvas.Loaded += delegate (object sender, RoutedEventArgs args) {
				UpdateFilterGraphCanvas ();
			};

			filterGraphCanvas.Background = Brushes.White;
			filterGraphCanvas.MouseMove += new MouseEventHandler (FilterGraphCanvas_MouseMove);
		}

		public Configurations Config { get; set; }
		public IList<string> AccountTypes { get { return StatusTypes.SourceTypes; } }

		#region Account

		private void  Account_Add_Click (object sender, RoutedEventArgs e)
		{
			IStatusSourceNodeInfo info = StatusTypes.GetInfo ((string)account_type.SelectedItem);
			IAccountInfo account = info.CreateAccountWithGUI (this);
			if (account == null)
				return;

			List<IAccountInfo> list = new List<IAccountInfo> ();
			if (Config.Accounts != null)
				list.AddRange (Config.Accounts);
			for (int i = 0; i < list.Count; i ++) {
				if (account.Equals (list[i])) {
					MessageBox.Show ("既に登録されているアカウントです", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}
			}
			list.Add (account);
			Config.Accounts = list.ToArray ();
			UpdateBindingTarget (this, "Config.Accounts");
			ConstructFilterGraph ();
			Config.Save ();
		}

		private void Account_Remove_Click (object sender, RoutedEventArgs e)
		{
			MessageBox.Show ("Not Implemented");
		}

		private void Account_ReAuth_Click (object sender, RoutedEventArgs e)
		{
			MessageBox.Show ("Not Implemented");
		}

		#endregion

		#region Filter Graph
		Dictionary<FilterGraphNodeKey, FilterGraphNodeShape> _vertices = new Dictionary<FilterGraphNodeKey, FilterGraphNodeShape> ();
		Dictionary<FilterGraphEdgeKey, Line> _edges = new Dictionary<FilterGraphEdgeKey,Line> ();

		static readonly string[] ViewerInputs = new string[] {"in"};
		DrawingLineInfo _drawing_line = null;

		void ConstructFilterGraph ()
		{
			Dictionary<FilterGraphNodeKey, FilterGraphNodeShape> vertices = new Dictionary<FilterGraphNodeKey, FilterGraphNodeShape> (_vertices);
			Dictionary<FilterGraphEdgeKey, Line> lines = new Dictionary<FilterGraphEdgeKey, Line> (_edges);

			const double SPACE = 50.0;
			object[][] objs = new object[][] {Config.Accounts, Config.PaneInfo.GetViewers ()};
			ElementType[] types = new ElementType[] {ElementType.Account, ElementType.Viewer};
			Thickness[] margines = new Thickness[] {new Thickness (0, 0, SPACE, 5.0), new Thickness (SPACE, 0, 0, 5.0)};
			HorizontalAlignment[] alignments = new HorizontalAlignment[] {HorizontalAlignment.Right, HorizontalAlignment.Left};
			for (int j = 0; j < objs.Length; j ++) {
				for (int i = 0; i < objs[j].Length; i ++) {
					IAccountInfo account = objs[j][i] as IAccountInfo;
					Configurations.PaneConfig viewer = objs[j][i] as Configurations.PaneConfig;
					FilterGraphNodeKey id = new FilterGraphNodeKey (types[j], account != null ? account.ID : viewer.Id);
					if (vertices.Remove (id)) continue;
					FilterGraphNodeShape node = new FilterGraphNodeShape {Key = id, Margin = margines[j], HorizontalAlignment = alignments[j]};
					node.PinMouseUp += Node_PinMouseUp;
					_vertices.Add (id, node);
					if (account != null) {
						node.Setup (account.SourceNodeInfo.SourceType + ": " + account.Summary,
							FilterGraphNodeShape.EmptyStrings, account.OutputStreams.Select (x => x.Name).ToArray ());
						filterGraphSourceList.Children.Add (node);
					} else {
						node.Setup (viewer.Caption, ViewerInputs, FilterGraphNodeShape.EmptyStrings);
						filterGraphViewerList.Children.Add (node);
					}
				}
			}
			foreach (FilterGraphNodeKey key in vertices.Keys) {
				filterGraphSourceList.Children.Remove (vertices[key]);
				filterGraphViewerList.Children.Remove (vertices[key]);
			}

			FilterGraphEdgeKey[] edges = Config.Edges;
			if (edges == null) edges = new FilterGraphEdgeKey[0];
			for (int i = 0; i < edges.Length; i ++) {
				FilterGraphEdgeKey edge = edges[i];
				if (lines.Remove (edge))
					continue;
				FilterGraphNodeShape srcShape, dstShape;
				if (!_vertices.TryGetValue (edge.SrcKey, out srcShape) || !_vertices.TryGetValue (edge.DstKey, out dstShape))
					continue;
				Line line = CreateGraphLine ();
				filterGraphCanvas.Children.Add (line);
				_edges.Add (edge, line);
			}
		}

		Line CreateGraphLine ()
		{
			Line line =  new Line ();
			line = new Line ();
			line.Stroke = Brushes.Black;
			line.StrokeThickness = 2;
			Canvas.SetZIndex (line, -1);
			return line;
		}

		void Node_PinMouseUp (object sender, FilterGraphNodeShape.PinMouseButtonEventArgs e)
		{
			Point pinPos = ((UIElement)sender).TranslatePoint (e.RelativePosition, filterGraphCanvas);
			Point point = e.GetPosition (filterGraphCanvas);

			if (_drawing_line == null) {
				Line line = CreateGraphLine ();
				line.X1 = pinPos.X;
				line.Y1 = pinPos.Y;
				line.X2 = point.X;
				line.Y2 = point.Y;
				filterGraphCanvas.Children.Add (line);

				_drawing_line = new DrawingLineInfo {
					Line = line,
					SrcKey = e.Key,
					SrcPinIndex = e.Index,
					IsSrcInput = e.IsInput
				};
			} else {
				DrawingLineInfo di = _drawing_line;
				Line l = _drawing_line.Line;
				_drawing_line = null;
				l.X2 = pinPos.X;
				l.Y2 = pinPos.Y;
				di.DstKey = e.Key;
				di.DstPinIndex = e.Index;
				di.IsDstInput = e.IsInput;
				di.Normalize ();
				FilterGraphEdgeKey ekey = di.ToKey ();
				if ((di.IsSrcInput == di.IsDstInput) || _edges.ContainsKey (ekey)) {
					if (_edges.ContainsKey (ekey)) {
						filterGraphCanvas.Children.Remove (_edges[ekey]);
						_edges.Remove (ekey);
					}
					filterGraphCanvas.Children.Remove (l);
				} else {
					_edges.Add (ekey, l);
				}
				Config.Edges = _edges.Keys.ToArray<FilterGraphEdgeKey> ();
				Config.Save ();
			}
		}

		void FilterGraphCanvas_MouseMove (object sender, MouseEventArgs e)
		{
			if (_drawing_line == null)
				return;

			Point point = e.GetPosition (filterGraphCanvas);
			_drawing_line.Line.X2 = point.X;
			_drawing_line.Line.Y2 = point.Y;
		}

		void UpdateFilterGraphCanvas ()
		{
			filterGraphCanvas.Width = gridFilterContainer.ActualWidth;
			filterGraphCanvas.Height = gridFilterContainer.ActualHeight;

			foreach (KeyValuePair<FilterGraphEdgeKey, Line> pair in _edges) {
				FilterGraphNodeShape srcShape, dstShape;
				if (!_vertices.TryGetValue (pair.Key.SrcKey, out srcShape) || !_vertices.TryGetValue (pair.Key.DstKey, out dstShape))
					continue;
				Point p0 = srcShape.GetSrcPinPoint (pair.Key.SrcPinIndex, filterGraphCanvas);
				Point p1 = dstShape.GetDstPinPoint (pair.Key.DstPinIndex, filterGraphCanvas);
				pair.Value.X1 = p0.X;
				pair.Value.Y1 = p0.Y;
				pair.Value.X2 = p1.X;
				pair.Value.Y2 = p1.Y;
			}
		}
		
		class DrawingLineInfo : FilterGraphEdgeKey
		{
			public bool IsSrcInput { get; set; }
			public bool IsDstInput { get; set; }
			public Line Line { get; set; }

			public void Normalize ()
			{
				if (!IsSrcInput)
					return;

				bool tmp0 = IsSrcInput;
				FilterGraphNodeKey tmp1 = SrcKey;
				int tmp2 = SrcPinIndex;

				IsSrcInput = IsDstInput;
				SrcKey = DstKey;
				SrcPinIndex = DstPinIndex;
				IsDstInput = tmp0;
				DstKey = tmp1;
				DstPinIndex = tmp2;
			}

			public FilterGraphEdgeKey ToKey ()
			{
				return new FilterGraphEdgeKey {
					SrcKey = this.SrcKey, SrcPinIndex = this.SrcPinIndex,
					DstKey = this.DstKey, DstPinIndex = this.DstPinIndex
				};
			}
		}
		#endregion

		#region Misc
		private void UpdateBindingTarget (object source, string path)
		{
			for (int i = 0; i < BindingGroup.BindingExpressions.Count; i ++) {
				BindingExpression exp = BindingGroup.BindingExpressions[i] as BindingExpression;
				if (exp == null) continue;
				if (exp.DataItem != source || exp.ParentBinding == null || exp.ParentBinding.Path == null) continue;
				if (path.Equals (exp.ParentBinding.Path.Path))
					exp.UpdateTarget ();
			}
		}
		#endregion

		#region Dependency Properties
		public static readonly DependencyProperty OverlapMarginProperty =
			DependencyProperty.Register ("OverlapMargin", typeof (Thickness), typeof (OptionWindow), new FrameworkPropertyMetadata (new Thickness (5), FrameworkPropertyMetadataOptions.AffectsMeasure, null));
		public Thickness OverlapMargin {
			get { return (Thickness)GetValue (OverlapMarginProperty); }
			set { SetValue (OverlapMarginProperty, value); }
		}
		#endregion

		private void OK_Click (object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void Cancel_Click (object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}
	}
}
