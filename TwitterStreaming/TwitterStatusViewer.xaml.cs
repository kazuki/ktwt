/*
 * Copyright (C) 2010 Kazuki Oikawa
 * 
 * Authors:
 *    Kazuki Oikawa
 *    @TKdo_ob
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
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

		#region Render Helper
		public static Regex TweetRegex = new Regex (
			@"(?<url>https?://[a-zA-Z0-9!#$%&'()=\-~^@`;\+:\*,\./\\?_]+)|" +
			@"(?<username>(?<=^|[^a-zA-Z0-9_])@[a-zA-Z0-9_]+)|" +
			@"(?<hashtag>(?<=^|[^a-zA-Z0-9\&\/])#[a-zA-Z0-9_]+)", RegexOptions.Compiled);
		static readonly IInlineImageUrlHandler[] InlineImageSites = new IInlineImageUrlHandler[] {
			new SimpleReplaceImageUrl ("http://twitpic.com/", "http://twitpic.com/show/thumb/", string.Empty),
			new SimpleReplaceImageUrl ("http://movapic.com/pic/", "http://image.movapic.com/pic/m_", ".jpeg"),
			new TweetPhotoHandler ()
		};

		public ToggleButton CreateFavoriteButton (Status s)
		{
			ToggleButton b = new ToggleButton ();
			Binding fontSizeBinding = CreateBinding (this, FontSizeProperty.Name, BindingMode.OneWay);
			b.SetBinding (ToggleButton.WidthProperty, fontSizeBinding);
			b.SetBinding (ToggleButton.HeightProperty, fontSizeBinding);
			Binding favBinding = CreateBinding (s, "IsFavorited", BindingMode.OneWay);
			b.SetBinding (ToggleButton.IsCheckedProperty, favBinding);
			b.Margin = new Thickness (0, 0, 3, 0);
			b.VerticalAlignment = VerticalAlignment.Center;
			b.Click += isFav_Click;
			b.Style = (Style)Resources["favoriteButton"];
			return b;
		}

		public Run CreateTextBlock (string text, FontWeight weight)
		{
			return CreateTextBlock (text, weight, null);
		}

		public Run CreateTextBlock (string text, FontWeight weight, DependencyProperty foreground)
		{
			Run x = new Run (text);
			x.FontWeight = weight;
			if (foreground != null)
				x.SetBinding (Run.ForegroundProperty, CreateBinding (this, foreground.Name, BindingMode.OneWay));
			return x;
		}

		public Hyperlink CreateHyperlink (string text, string url, DependencyProperty foreground, FontWeight weight, RoutedEventHandler handler)
		{
			Hyperlink link = new Hyperlink ();
			link.SetBinding (Hyperlink.ForegroundProperty, CreateBinding (this, foreground.Name, BindingMode.OneWay));
			link.Inlines.Add (text);
			link.Tag = url;
			link.Click += handler;
			link.FontWeight = weight;
			return link;
		}

		public static Binding CreateBinding (object source, string path, BindingMode mode)
		{
			Binding binding = new Binding (path);
			binding.Source = source;
			binding.Mode = mode;
			return binding;
		}

		public void CreateTweetBody (string text, InlineCollection inlines)
		{
			Match m = TwitterStatusViewer.TweetRegex.Match (text);
			int last = 0;
			List<Hyperlink> images = null;
			while (m.Success) {
				inlines.Add (text.Substring (last, m.Index - last));
				if (m.Success) {
					Hyperlink link = CreateHyperlink (m.Value, m.Value, TwitterStatusViewer.LinkForegroundProperty, this.FontWeight, Hyperlink_Click);
					inlines.Add (link);

					foreach (IInlineImageUrlHandler handler in InlineImageSites) {
						string picurl = handler.Process (m.Value);
						if (picurl == null)
							continue;
						Hyperlink imgLink = new Hyperlink {Tag = m.Value};
						imgLink.Click += Hyperlink_Click;
						imgLink.Inlines.Add (new Image {
							Source = new BitmapImage (new Uri (picurl)),
							Stretch = Stretch.Uniform,
							MaxWidth = 50,
							MaxHeight = 50
						});
						if (images == null) images = new List<Hyperlink> ();
						images.Add (imgLink);
						break;
					}
				}

				last = m.Index + m.Length;
				m = m.NextMatch ();
			}
			inlines.Add (text.Substring (last));

			if (images != null) {
				inlines.Add (Environment.NewLine);
				for (int i = 0; i < images.Count; i ++)
					inlines.Add (images[i]);
			}
		}

		#region Inline Image Site Preferences
		interface IInlineImageUrlHandler
		{
			string Process (string url);
		}
		sealed class SimpleReplaceImageUrl : IInlineImageUrlHandler
		{
			string _prefix, _new_prefix, _suffix;

			public SimpleReplaceImageUrl (string old_prefix, string new_prefix, string append_suffix)
			{
				_prefix = old_prefix;
				_new_prefix = new_prefix;
				_suffix = append_suffix;
			}

			public string Process (string url)
			{
				if (!url.StartsWith (_prefix))
					return null;
				return url.Replace (_prefix, _new_prefix) + _suffix;
			}
		}
		sealed class TweetPhotoHandler : IInlineImageUrlHandler
		{
			const string _prefix = "http://tweetphoto.com/";
			static Regex _regex = new Regex (_prefix + @"(?<id>\d+)", RegexOptions.Compiled);

			public string Process (string url)
			{
				if (!url.StartsWith (_prefix))
					return null;
				Match m = _regex.Match (url);
				ulong id;
				if (!m.Success || !m.Groups["id"].Success || !ulong.TryParse (m.Groups["id"].Value, out id))
					return null;
				return "http://cdn.cloudfiles.mosso.com/c54112/x2_" + id.ToString ("x");
			}
		}
		#endregion
		#endregion

		#region Event Handlers
		public void Hyperlink_Click (object sender, RoutedEventArgs e)
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
		#endregion

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

		#region Icon
		public static readonly DependencyProperty IconSizeProperty =
			DependencyProperty.Register ("IconSize", typeof (int), typeof (TwitterStatusViewer), new FrameworkPropertyMetadata (32, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, IconSizePropertyChanged));
		public int IconSize {
			get { return (int)GetValue (IconSizeProperty); }
			set {
				SetValue (IconSizeProperty, value);
				IconVisibilityCheck ();
			}
		}

		public static readonly DependencyProperty MinimumIconSizeProperty =
			DependencyProperty.Register ("MinimumIconSize", typeof (int), typeof (TwitterStatusViewer), new FrameworkPropertyMetadata (8, FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsRender, IconSizePropertyChanged));
		public int MinimumIconSize {
			get { return (int)GetValue (MinimumIconSizeProperty); }
			set {
				SetValue (MinimumIconSizeProperty, value);
				IconVisibilityCheck ();
			}
		}

		static void IconSizePropertyChanged (DependencyObject d, DependencyPropertyChangedEventArgs e) {
			TwitterStatusViewer self = (TwitterStatusViewer)d;
			self.IconVisibilityCheck ();
		}

		void IconVisibilityCheck ()
		{
			//userImage.Visibility = (IconSize >= MinimumIconSize ? Visibility.Visible : Visibility.Collapsed);
		}
		#endregion

		#region View Style
		const FrameworkPropertyMetadataOptions AffectsAll = FrameworkPropertyMetadataOptions.AffectsArrange | FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;

		public static readonly DependencyProperty ViewModeProperty =
			DependencyProperty.Register ("ViewMode", typeof (StatusViewMode), typeof (TwitterStatusViewer), new FrameworkPropertyMetadata (StatusViewMode.Normal, AffectsAll, PropertyChanged));
		public StatusViewMode ViewMode {
			get { return (StatusViewMode)GetValue (ViewModeProperty); }
			set { SetValue (ViewModeProperty, value); }
		}

		static void PropertyChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
		}
		#endregion

		#region Misc
		public Status Status {
			get { return DataContext as Status; }
		}
		#endregion
	}

	public enum StatusViewMode
	{
		Normal,
		Compact
	}

	public class TweetProfileImage : Image
	{
		public static readonly DependencyProperty OwnerProperty =
			DependencyProperty.Register ("Owner", typeof (TwitterStatusViewer), typeof (TweetProfileImage), new PropertyMetadata (new PropertyChangedCallback (OnOwnerChanged)));
		public TwitterStatusViewer Owner {
			get { return (TwitterStatusViewer)GetValue (OwnerProperty); }
			set { SetValue (OwnerProperty, value); }
		}

		private static void OnOwnerChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			TweetProfileImage self = (TweetProfileImage)d;
			self.Render ();
		}

		void Render ()
		{
			if (Owner == null) {
				Source = null;
				return;
			}
			Status s = Owner.DataContext as Status;
			if (s == null)
				return;
			if (s.RetweetedStatus != null)
				s = s.RetweetedStatus;
			Source = IconCache.GetImage (s.User.ID, s.User.ProfileImageUrl);
		}
	}

	public abstract class TweetTextBlockBase : TextBlock
	{
		public static readonly DependencyProperty OwnerProperty =
			DependencyProperty.Register ("Owner", typeof (TwitterStatusViewer), typeof (TweetTextBlockBase), new PropertyMetadata (new PropertyChangedCallback (OnOwnerChanged)));
		public TwitterStatusViewer Owner {
			get { return (TwitterStatusViewer)GetValue (OwnerProperty); }
			set { SetValue (OwnerProperty, value); }
		}

		private static void OnOwnerChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			TweetTextBlockBase self = (TweetTextBlockBase)d;
			self.Render ();
		}

		protected abstract void Render ();
	}

	public class TweetNameTextBlock : TweetTextBlockBase
	{
		protected override void Render ()
		{
			InlineCollection inlines = Inlines;
			inlines.Clear ();

			TwitterStatusViewer v = Owner;
			if (v == null) return;
			Status s = v.DataContext as Status;
			if (s == null) return;
			Status s1 = s;
			if (s.RetweetedStatus != null)
				s = s.RetweetedStatus;

			FontWeight defWeight = this.FontWeight;
			RoutedEventHandler defLinkHandler = new RoutedEventHandler (v.Hyperlink_Click);
			DependencyProperty nameFg = TwitterStatusViewer.NameForegroundProperty;

			Inlines.Add (v.CreateHyperlink (s.User.ScreenName + " [" + s.User.Name + "]", "/" + s.User.ScreenName, nameFg, defWeight, defLinkHandler));
			if (!string.IsNullOrEmpty (s.InReplyToScreenName)) {
				Inlines.Add (v.CreateTextBlock (" in reply to ", FontWeights.Normal));
				Inlines.Add (v.CreateHyperlink ("@" + s.InReplyToScreenName, "/" + s.InReplyToScreenName + (s.InReplyToStatusId == 0 ? string.Empty : "/status/" + s.InReplyToStatusId.ToString ()), nameFg, defWeight, defLinkHandler));
			}
			if (s != s1) {
				Inlines.Add (v.CreateTextBlock (" retweeted by ", FontWeights.Normal));
				Inlines.Add (v.CreateHyperlink ("@" + s1.User.ScreenName, "/" + s1.User.ScreenName, nameFg, defWeight, defLinkHandler));
			}
			if (s.Source != null) {
				int p1 = s.Source.IndexOf ('>');
				int p2 = s.Source.IndexOf ('<', Math.Max (0, p1));
				string appName = s.Source;
				if (p1 >= 0 && p2 > 0)
					appName = s.Source.Substring (p1 + 1, p2 - p1 - 1);
				p1 = s.Source.IndexOf ('\"');
				p2 = s.Source.IndexOf ('\"', Math.Max (0, p1 + 1));
				Inlines.Add (v.CreateTextBlock (" from ", FontWeights.Normal));
				if (p1 >= 0 && p2 > 0) {
					Inlines.Add (v.CreateHyperlink (appName, s.Source.Substring (p1 + 1, p2 - p1 - 1), nameFg, defWeight, defLinkHandler));
				} else {
					Inlines.Add (appName);
				}
			}
			Inlines.Add (v.CreateTextBlock (" (" + s.CreatedAt.ToString ("MM/dd HH:mm:ss") + ")", FontWeights.Normal));
		}
	}

	public class TweetBodyTextBlock : TweetTextBlockBase
	{
		protected override void Render ()
		{
			InlineCollection inlines = Inlines;
			inlines.Clear ();

			TwitterStatusViewer v = Owner;
			if (v == null) return;
			Status s = v.DataContext as Status;
			if (s == null) return;
			Status s1 = s;
			if (s.RetweetedStatus != null)
				s = s.RetweetedStatus;

			v.CreateTweetBody (s.Text, inlines);
		}
	}

	public class TweetCompactTextBlock : TweetTextBlockBase
	{
		protected override void Render ()
		{
			InlineCollection inlines = Inlines;
			while (inlines.Count > 1)
				inlines.Remove (inlines.LastInline);

			TwitterStatusViewer v = Owner;
			if (v == null) return;
			Status s = v.DataContext as Status;
			if (s == null) return;
			Status s1 = s;
			if (s.RetweetedStatus != null)
				s = s.RetweetedStatus;

			// 改行や連続する空白を削除
			string text = s.Text;
			StringBuilder sb = new StringBuilder (text.Length);
			int state = 0;
			for (int i = 0; i < text.Length; i ++) {
				char c = text[i];
				if (c == '\r' || c == '\n') continue;
				if (char.IsWhiteSpace (c)) {
					if (state == 1) continue;
					state = 1;
				} else {
					state = 0;
				}
				sb.Append (text[i]);
			}
			text = sb.ToString ();

			// Favoriteアイコン
			ToggleButton favBtn = v.CreateFavoriteButton (s);
			favBtn.Margin = new Thickness (0, 0, 3, 0);
			inlines.Add (favBtn);

			// 名前を追加
			RoutedEventHandler defLinkHandler = new RoutedEventHandler (v.Hyperlink_Click);
			DependencyProperty nameFg = TwitterStatusViewer.NameForegroundProperty;
			inlines.Add (v.CreateHyperlink (s.User.ScreenName, "/" + s.User.ScreenName, nameFg, FontWeights.Bold, defLinkHandler));
			inlines.Add (" ");

			// 本文を追加
			v.CreateTweetBody (text, inlines);

			// 返信情報を追加
			if (!string.IsNullOrEmpty (s.InReplyToScreenName)) {
				inlines.Add (v.CreateTextBlock (" in reply to ", FontWeights.Normal, nameFg));
				inlines.Add (v.CreateHyperlink ("@" + s.InReplyToScreenName, "/" + s.InReplyToScreenName + (s.InReplyToStatusId == 0 ? string.Empty : "/status/" + s.InReplyToStatusId.ToString ()), nameFg, FontWeights.Bold, defLinkHandler));
			}
			if (s != s1) {
				inlines.Add (v.CreateTextBlock (" RT by ", FontWeights.Normal, nameFg));
				inlines.Add (v.CreateHyperlink ("@" + s1.User.ScreenName, "/" + s1.User.ScreenName, nameFg, FontWeights.Bold, defLinkHandler));
			}

		}
	}
}
