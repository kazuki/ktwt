using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ktwt.ui
{
	public class FilterGraphNodeShape : UserControl
	{
		public static readonly string[] EmptyStrings = new string[0];
		public static readonly PinInfo[] EmptyPins = new PinInfo[0];
		Grid gridMain;
		string _text = string.Empty;
		PinInfo[] _inputs = EmptyPins, _outputs = EmptyPins;

		public event EventHandler<PinMouseButtonEventArgs> PinMouseDown;
		public event EventHandler<PinMouseButtonEventArgs> PinMouseUp;

		public FilterGraphNodeShape ()
		{
			Border outer = new Border ();
			gridMain = new Grid ();
			outer.Child = gridMain;
			this.AddChild (outer);

			outer.BorderBrush = Brushes.Black;
			outer.BorderThickness = new Thickness (1);
			this.HorizontalAlignment = HorizontalAlignment.Center;
		}

		public FilterGraphNodeKey Key { get; set; }

		public string Text {
			get { return _text; }
			set {
				_text = value;
				Setup (value, null, null);
			}
		}

		PinInfo[] CreateEmptyPinArray (string[] labelArray, bool isInput)
		{
			PinInfo[] array = new PinInfo[labelArray.Length];
			for (int i = 0; i < array.Length; i ++) {
				array[i] = new PinInfo {
					IsInput = isInput, IsOutput = !isInput,
					Label = labelArray[i],
					PinIndex = i
				};
			}
			return array;
		}

		public void Setup (string text, string[] inputs, string[] outputs)
		{
			// Config
			int pin_width = 20, pin_height = (int)Math.Ceiling (FontSize / 1.5);
			int pin_half_width = pin_width / 2;
			int margin = 5, pin_lbl_vmargin = 2;

			// Update Properties
			_text = text;
			if (inputs != null)
				_inputs = CreateEmptyPinArray (inputs, true);
			if (outputs != null)
				_outputs = CreateEmptyPinArray (outputs, false);

			// Reset
			gridMain.Children.Clear ();
			gridMain.ColumnDefinitions.Clear ();
			gridMain.RowDefinitions.Clear ();

			// Construct Grid
			int main_lbl_offset = (inputs.Length == 0 ? 0 : 2);
			int rows = Math.Max (1, Math.Max (inputs.Length, outputs.Length));
			int cols = 1 + main_lbl_offset + (outputs.Length == 0 ? 0 : 2);
			for (int i = 0; i < rows; i ++) gridMain.RowDefinitions.Add (new RowDefinition {Height = GridLength.Auto});
			for (int i = 0; i < cols; i ++) gridMain.ColumnDefinitions.Add (new ColumnDefinition {Width = GridLength.Auto});
			gridMain.Margin = new Thickness (inputs.Length == 0 ? 0 : -pin_half_width, 0, outputs.Length == 0 ? 0 : -pin_half_width, 0);
			this.Padding = new Thickness (-gridMain.Margin.Left, 0, -gridMain.Margin.Right, 0);

			// Construct Label
			Thickness tb_margin = new Thickness (margin, pin_lbl_vmargin, margin, pin_lbl_vmargin);
			TextBlock tb = new TextBlock {VerticalAlignment = VerticalAlignment.Center, Text = text, Margin = tb_margin};
			Grid.SetColumn (tb, main_lbl_offset);
			Grid.SetRow (tb, 0);
			Grid.SetRowSpan (tb, rows);
			gridMain.Children.Add (tb);

			// Construct PIN
			int pin_col = 0, label_col = 1;
			PinInfo[] pins = _inputs;
			for (int pin_type = 0; pin_type < 2; pin_type ++) {
				for (int i = 0; i < pins.Length; i ++) {
					Rectangle rect = new Rectangle {Height = pin_height, Width = pin_width, Fill = Brushes.Black, VerticalAlignment = VerticalAlignment.Center};
					tb = new TextBlock {HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = tb_margin, Text = pins[i].Label};
					pins[i].Element = rect;

					Grid.SetColumn (rect, pin_col);
					Grid.SetRow (rect, i);
					Grid.SetColumn (tb, label_col);
					Grid.SetRow (tb, i);

					gridMain.Children.Add (rect);
					gridMain.Children.Add (tb);
					rect.Tag = pins[i];
					rect.MouseDown += new MouseButtonEventHandler (Pin_MouseDown);
					rect.MouseUp += new MouseButtonEventHandler (Pin_MouseUp);
					rect.MouseEnter += new MouseEventHandler (Pin_MouseEnter);
					rect.MouseLeave += new MouseEventHandler (Pin_MouseLeave);
				}

				// next
				pin_col = 2 + main_lbl_offset;
				label_col = 1 + main_lbl_offset;
				pins = _outputs;
			}
		}

		void Pin_MouseLeave (object sender, MouseEventArgs e)
		{
			(sender as Rectangle).Fill = Brushes.Black;
		}

		void Pin_MouseEnter (object sender, MouseEventArgs e)
		{
			(sender as Rectangle).Fill = Brushes.Red;
		}

		void Pin_MouseUp (object sender, MouseButtonEventArgs e)
		{
			Pin_MouseEvent (sender, PinMouseUp, e);
		}

		void Pin_MouseDown (object sender, MouseButtonEventArgs e)
		{
			Pin_MouseEvent (sender, PinMouseDown, e);
		}

		void Pin_MouseEvent (object sender, EventHandler<PinMouseButtonEventArgs> handler, MouseButtonEventArgs e)
		{
			if (handler == null)
				return;
			FrameworkElement fe = (FrameworkElement)sender;
			PinInfo info = (PinInfo)fe.Tag;
			Point pos = GetPinPoint (info, this);
			PinMouseButtonEventArgs args = new PinMouseButtonEventArgs (Key, info, pos, e);
			handler (this, args);
		}

		public Point GetSrcPinPoint (int index, UIElement relativeTo)
		{
			return GetPinPoint (_outputs[index], relativeTo);
		}

		public Point GetDstPinPoint (int index, UIElement relativeTo)
		{
			return GetPinPoint (_inputs[index], relativeTo);
		}

		public Point GetPinPoint (PinInfo pin, UIElement relativeTo)
		{
			if (relativeTo == null)
				relativeTo = this;
			Point pos = pin.Element.TranslatePoint (new Point (0, 0), relativeTo);
			return new Point (pos.X + (pin.IsInput ? 0.0 : pin.Element.ActualWidth),
				pos.Y + pin.Element.ActualHeight / 2.0);
		}

		public class PinInfo
		{
			public bool IsInput { get; set; }
			public bool IsOutput { get; set; }
			public int PinIndex { get; set; }
			public FrameworkElement Element { get; set; }
			public string Label { get; set; }

			public PinInfo ()
			{
				IsInput = false;
				IsOutput = false;
				PinIndex = -1;
				Element = null;
				Label = string.Empty;
			}

			public override string ToString ()
			{
				return (IsInput ? "input" : IsOutput ? "output" : "none") + "-" + PinIndex.ToString ();
			}
		}

		public class PinMouseButtonEventArgs : MouseButtonEventArgs
		{
			public PinMouseButtonEventArgs (FilterGraphNodeKey key, PinInfo pinInfo, Point relPos, MouseButtonEventArgs baseArgs) : 
				base (baseArgs.MouseDevice, baseArgs.Timestamp, baseArgs.ChangedButton, baseArgs.StylusDevice)
			{
				this.Key = key;
				this.RoutedEvent = baseArgs.RoutedEvent;
				this.RelativePosition = relPos;
				this.PinInfo = pinInfo;
			}

			public FilterGraphNodeKey Key { get; private set; }
			private PinInfo PinInfo { get; set; }
			public int Index {
				get { return PinInfo.PinIndex; }
			}
			public bool IsInput {
				get { return PinInfo.IsInput; }
			}
			public bool IsOutput {
				get { return PinInfo.IsOutput; }
			}
			public Point RelativePosition { get; private set; }
		}
	}
}
