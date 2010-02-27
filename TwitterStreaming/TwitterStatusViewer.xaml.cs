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
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class TwitterStatusViewer : UserControl
	{
		public TwitterStatusViewer ()
		{
			InitializeComponent ();
		}

		public event EventHandler<LinkClickEventArgs> LinkClick;
		public event EventHandler<RoutedEventArgs> FavoriteIconClick;

		public static readonly DependencyProperty StatusProperty =
			DependencyProperty.Register ("Status", typeof (Status), typeof (TwitterStatusViewer), new PropertyMetadata (null, StatusPropertyChanged));
		public Status Status {
			get { return (Status)GetValue (StatusProperty); }
			set { SetValue (StatusProperty, value); }
		}

		static Regex _urlRegex = new Regex (@"(?<url>https?://[a-zA-Z0-9!#$%&'()=\-~^@`;\+:\*,\./\\?_]+)|(?<username>@[a-zA-Z0-9_]+)|(?<hashtag>#[\w]+)", RegexOptions.Compiled);
		static void StatusPropertyChanged (DependencyObject d, DependencyPropertyChangedEventArgs e) {
			TwitterStatusViewer self = (TwitterStatusViewer)d;
			Status s = (Status)e.NewValue;
			if (s == null) return;
			Status s1 = s;
			if (s.RetweetedStatus != null)
				s = s.RetweetedStatus;

			// Reset
			self.userImage.Source = null;
			self.nameTextBlock.Inlines.Clear ();
			self.postTextBlock.Inlines.Clear ();

			if (s.User.ProfileImageUrl != null)
				self.userImage.Source = new BitmapImage (new Uri (s.User.ProfileImageUrl));

			FontWeight defWeight = self.nameTextBlock.FontWeight;
			RoutedEventHandler defLinkHandler = new RoutedEventHandler (self.Hyperlink_Click);
			InlineCollection nameInlines = self.nameTextBlock.Inlines;
			nameInlines.Add (self.CreateHyperlink (s.User.ScreenName + " [" + s.User.Name + "]", "/" + s.User.ScreenName, NameForegroundProperty, defWeight, defLinkHandler));
			if (!string.IsNullOrEmpty (s.InReplyToScreenName)) {
				nameInlines.Add (CreateTextBlock (" in reply to ", FontWeights.Normal));
				nameInlines.Add (self.CreateHyperlink ("@" + s.InReplyToScreenName, "/" + s.InReplyToScreenName + (s.InReplyToStatusId == 0 ? string.Empty : "/status/" + s.InReplyToStatusId.ToString ()), NameForegroundProperty, defWeight, defLinkHandler));
			}
			if (s != s1) {
				nameInlines.Add (CreateTextBlock (" retweeted by ", FontWeights.Normal));
				nameInlines.Add (self.CreateHyperlink ("@" + s1.User.ScreenName, "/" + s1.User.ScreenName, NameForegroundProperty, defWeight, defLinkHandler));
			}
			if (s.Source != null) {
				int p1 = s.Source.IndexOf ('>');
				int p2 = s.Source.IndexOf ('<', Math.Max (0, p1));
				string appName = s.Source;
				if (p1 >= 0 && p2 > 0)
					appName = s.Source.Substring (p1 + 1, p2 - p1 - 1);
				p1 = s.Source.IndexOf ('\"');
				p2 = s.Source.IndexOf ('\"', Math.Max (0, p1 + 1));
				nameInlines.Add (CreateTextBlock (" from ", FontWeights.Normal));
				if (p1 >= 0 && p2 > 0) {
					nameInlines.Add (self.CreateHyperlink (appName, s.Source.Substring (p1 + 1, p2 - p1 - 1), NameForegroundProperty, defWeight, defLinkHandler));
				} else {
					nameInlines.Add (appName);
				}
			}
			nameInlines.Add (CreateTextBlock (" (" + s.CreatedAt.ToString ("MM/dd HH:mm:ss") + ")", FontWeights.Normal));

			InlineCollection inlines = self.postTextBlock.Inlines;
			Match m = _urlRegex.Match (s.Text);
			int last = 0;
			while (m.Success) {
				inlines.Add (s.Text.Substring (last, m.Index - last));
				if (m.Success) {
					Hyperlink link = self.CreateHyperlink (m.Value, m.Value, LinkForegroundProperty, self.postTextBlock.FontWeight, self.Hyperlink_Click);
					inlines.Add (link);
					/*if (m.Groups["url"].Success) {
						link.Foreground = self.LinkForeground;
					} else if (m.Groups["username"].Success) {
						link.Foreground = self.LinkForeground;
					} else if (m.Groups["hashtag"].Success) {
						link.Foreground = self.LinkForeground;
					}*/
				}

				last = m.Index + m.Length;
				m = m.NextMatch ();
			}
			inlines.Add (s.Text.Substring (last));
		}

		static Run CreateTextBlock (string text, FontWeight weight)
		{
			Run x = new Run (text);
			x.FontWeight = weight;
			return x;
		}

		Hyperlink CreateHyperlink (string text, string url, DependencyProperty foreground, FontWeight weight, RoutedEventHandler handler)
		{
			Hyperlink link = new Hyperlink ();
			Binding binding = new Binding (foreground.Name);
			binding.Source = this;
			link.SetBinding (Hyperlink.ForegroundProperty, binding);
			link.Inlines.Add (text);
			link.Tag = url;
			link.Click += handler;
			return link;
		}

		void Hyperlink_Click (object sender, RoutedEventArgs e)
		{
			if (LinkClick != null)
				LinkClick (this, new LinkClickEventArgs ((sender as Hyperlink).Tag as string));
		}

		private void isFav_Click (object sender, RoutedEventArgs e)
		{
			if (FavoriteIconClick == null)
				return;
			try {
				FavoriteIconClick (DataContext, e);
			} catch {}
		}

		#region Colors
		public static readonly DependencyProperty NameForegroundProperty =
			DependencyProperty.Register ("NameForeground", typeof (Brush), typeof (TwitterStatusViewer), new FrameworkPropertyMetadata (Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender, null));
		public Brush NameForeground {
			get { return (Brush)GetValue (NameForegroundProperty); }
			set { SetValue (NameForegroundProperty, value); }
		}

		public static readonly DependencyProperty LinkForegroundProperty =
			DependencyProperty.Register ("LinkForeground", typeof (Brush), typeof (TwitterStatusViewer), new FrameworkPropertyMetadata (Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender, null));
		public Brush LinkForeground {
			get { return (Brush)GetValue (LinkForegroundProperty); }
			set { SetValue (LinkForegroundProperty, value); }
		}
		#endregion
	}
}
