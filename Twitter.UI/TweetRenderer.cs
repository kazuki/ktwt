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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using ktwt.StatusStream;
using ktwt.ui;

namespace ktwt.Twitter.ui
{
	class TweetRenderer : IStatusRenderer
	{
		static TweetRenderer () { Instance = new TweetRenderer (); }
		public static TweetRenderer Instance { get; private set; }

		const string TweetRegetUrlGroupName = "url";
		const string TweetRegetUserNameGroupName = "username";
		const string TweetRegetHashTagGroupName = "hashtag";
		static Regex TweetRegex = new Regex (
			@"(?<" + TweetRegetUrlGroupName+ @">https?://[a-zA-Z0-9!#$%&'()=\-~^@`;\+:\*,\./\\?_]+)|" +
			@"(?<" + TweetRegetUserNameGroupName + @">(?<=^|[^a-zA-Z0-9_])@[a-zA-Z0-9_]+)|" +
			@"(?<" + TweetRegetHashTagGroupName + @">(?<=^|[^a-zA-Z0-9\&\/])#[a-zA-Z0-9_]+)", RegexOptions.Compiled);
		const string IN_REPLY_TO = " in reply to ";
		const string RETWEETED_BY = " retweeted by  ";
		const string FROM = " from ";

		TweetRenderer ()
		{
			FontFamily ff = SystemFonts.MessageFontFamily;
			Typeface tf = new Typeface (ff, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			Typeface btf = new Typeface (ff, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
			double fontSize = 10.5;
			CultureInfo ci = CultureInfo.CurrentUICulture;

			HeaderDefaultTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.DarkBlue, fontSize, fontSize, null, null, ci);
			HeaderNameTextRunProperties = new BasicTextRunProperties (btf, null, Brushes.DarkBlue, fontSize, fontSize, TextDecorations.Underline, null, ci);
			HeaderParagraphProperties = new BasicTextParagraphProperties (HeaderDefaultTextRunProperties,
				false, FlowDirection.LeftToRight, 0.0, 0.0, TextAlignment.Left, null, TextWrapping.NoWrap);

			DefaultTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.Black, fontSize, fontSize, null, null, ci);
			UrlTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.Blue, fontSize, fontSize, TextDecorations.Underline, null, ci);
			UserNameTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.Red, fontSize, fontSize, TextDecorations.Underline, null, ci);
			HashTagRunProperties = new BasicTextRunProperties (tf, null, Brushes.Green, fontSize, fontSize, TextDecorations.Underline, null, ci);

			ParagraphProperties = new BasicTextParagraphProperties (DefaultTextRunProperties,
				false, FlowDirection.LeftToRight, 0.0, 0.0, TextAlignment.Left, null, TextWrapping.Wrap);
		}

		public IDecoratedStatus Decorate (StatusBase status)
		{
			Status s = (Status)status;
			Status s0 = s;
			if (s.RetweetedStatus != null)
				s = s.RetweetedStatus;
			User u = (User)s.User;
			string text = s.Text;
			Match m = TweetRegex.Match (text);
			int last = 0;
			List<DecoratedText> inlines = new List<DecoratedText> ();
			while (m.Success) {
				if (m.Index > last)
					inlines.Add (new DecoratedText (DefaultTextRunProperties, text, last, m.Index - last, false));
				if (m.Success) {
					TextRunProperties prop = 
						m.Groups[TweetRegetUrlGroupName].Success ? UrlTextRunProperties
						: m.Groups[TweetRegetUserNameGroupName].Success ? UserNameTextRunProperties
						: m.Groups[TweetRegetHashTagGroupName].Success ? HashTagRunProperties : DefaultTextRunProperties;
					Capture c = m.Captures[0];
					inlines.Add (new DecoratedText (prop, text, c.Index, c.Length, false));
				}
				last = m.Index + m.Length;
				m = m.NextMatch ();
			}
			if (last < text.Length)
				inlines.Add (new DecoratedText (DefaultTextRunProperties, text, last, false));
			DecoratedText[] texts = inlines.ToArray ();
			inlines.Clear ();

			inlines.Add (new DecoratedText (HeaderNameTextRunProperties, u.ScreenName + " [" + u.Name + "]", false));
			if (s.InReplyToScreenName != null && s.InReplyToScreenName.Length > 0) {
				inlines.Add (new DecoratedText (HeaderDefaultTextRunProperties, IN_REPLY_TO, false));
				inlines.Add (new DecoratedText (HeaderNameTextRunProperties, "@" + s.InReplyToScreenName, false));
			}
			if (s != s0) {
				inlines.Add (new DecoratedText (HeaderDefaultTextRunProperties, RETWEETED_BY, false));
				inlines.Add (new DecoratedText (HeaderNameTextRunProperties, "@" + ((User)s0.User).ScreenName, false));
			}
			if (s.Source != null && s.Source.Length > 0) {
				int p1 = s.Source.IndexOf ('>');
				int p2 = s.Source.IndexOf ('<', Math.Max (0, p1));
				/*int p3 = s.Source.IndexOf ('\"');
				int p4 = s.Source.IndexOf ('\"', Math.Max (0, p3 + 1));
				inlines.Add (new DecoratedText (HeaderDefaultTextRunProperties, FROM, false));
				if (p3 >= 0 && p4 > 0) {
					string url = s.Source.Substring (p3 + 1, p4 - p3 - 1);
				}*/
				inlines.Add (new DecoratedText (HeaderDefaultTextRunProperties, FROM, false));
				if (p1 >= 0 && p2 > 0)
					inlines.Add (new DecoratedText (HeaderNameTextRunProperties, s.Source, p1 + 1, p2 - p1 - 1, false));
				else
					inlines.Add (new DecoratedText (HeaderNameTextRunProperties, s.Source, false));
			}
			inlines.Add (new DecoratedText (HeaderDefaultTextRunProperties, " (" + s.CreatedAt.ToString ("MM/dd HH:mm:ss") + ")", false));

			return new FormattedText (status, HeaderParagraphProperties, ParagraphProperties, inlines.ToArray (), texts, text.Length);
		}

		public TextRunProperties HeaderDefaultTextRunProperties { get; set; }
		public TextRunProperties HeaderNameTextRunProperties { get; set; }
		public TextParagraphProperties HeaderParagraphProperties { get; set; }

		public TextRunProperties DefaultTextRunProperties { get; set; }
		public TextRunProperties UrlTextRunProperties { get; set; }
		public TextRunProperties UserNameTextRunProperties { get; set; }
		public TextRunProperties HashTagRunProperties { get; set; }
		public TextParagraphProperties ParagraphProperties { get; set; }

		public double Render (IStatusRendererOwner owner, DrawingContext drawingContext, IDecoratedStatus item, double offset_y, double width)
		{
			FormattedText text = (FormattedText)item;
			Status s = (Status)item.Status;
			User u = s.User as User;

			double padding_left = 5;
			double padding_top = 2;
			double padding_right = 5;
			double x, y = offset_y + padding_top;
			double text_width;
			int pos = 0;
			CustomTextSource headerSource = new CustomTextSource (text.Headers);
			CustomTextSource bodySource = new CustomTextSource (text.Texts);

			// draw profile image
			Size iconSize = owner.ImageCache.ImageSize;
			x = padding_left;
			ImageSource img = null;
			if (text.ProfileImage != null) {
				img = text.ProfileImage.Target as ImageSource;
				if (img != null && (img.Width != iconSize.Width || img.Height != iconSize.Height)) {
					text.ProfileImage = null;
					img = null;
				}
			}
			if (img == null) {
				img = owner.ImageCache.LoadCache (u.ProfileImageUrl);
				text.ProfileImage = new WeakReference (img);
			}
			if (img != null) {
				try {
					drawingContext.DrawImage (img, new Rect (x, y, iconSize.Width, iconSize.Height));
				} catch {}
			}

			// draw header
			x = padding_left * 2 + iconSize.Width;
			text_width = width - x;
			using (TextLine line = owner.TextFormatter.FormatLine (headerSource, pos, text_width, text.HeaderParagraphProperties, null)) {
				line.Draw (drawingContext, new Point (x, y), InvertAxes.None);
				y += line.Height;
			}

			// draw text
			x = padding_left * 2 + iconSize.Width;
			text_width = width - x - padding_right;
			while (pos < text.TextLength) {
				using (TextLine line = owner.TextFormatter.FormatLine (bodySource, pos, text_width, text.ParagraphProperties, null)) {
					line.Draw (drawingContext, new Point (x, y), InvertAxes.None);
					pos += line.Length;
					y += line.Height;
				}
			}

			return Math.Max (y - offset_y, iconSize.Height);
		}

		#region Internal Classes
		class DecoratedText
		{
			public DecoratedText (TextRunProperties prop, string text, bool copy, object tag = null) : this (prop, text, 0, text.Length, copy, tag) {}
			public DecoratedText (TextRunProperties prop, string text, int offset, bool copy, object tag = null) : this (prop, text, offset, text.Length - offset, copy, tag) {}
			public DecoratedText (TextRunProperties prop, string text, int offset, int len, bool copy, object tag = null)
			{
				Properties = prop;
				Tag = tag;
				if (copy) {
					Offset = 0;
					Text = text.Substring (offset, len);
					Length = len;
				} else {
					Text = text;
					Offset = offset;
					Length = len;
				}
			}

			public TextRunProperties Properties { get; private set; }
			public string Text { get; private set; }
			public int Offset { get; private set; }
			public int Length { get; private set; }
			public object Tag { get; set; }

			public override string ToString ()
			{
				if (Offset == 0 && Length == Text.Length)
					return Text;
				return Text.Substring (Offset, Length);
			}
		}
		class FormattedText : IDecoratedStatus
		{
			public FormattedText (StatusBase status, TextParagraphProperties header_prop, TextParagraphProperties prop, DecoratedText[] headers, DecoratedText[] texts, int text_length)
			{
				Status = status;
				HeaderParagraphProperties = header_prop;
				ParagraphProperties = prop;
				Headers = headers;
				Texts = texts;
				TextLength = text_length;
			}

			public TextParagraphProperties HeaderParagraphProperties { get; set; }
			public TextParagraphProperties ParagraphProperties { get; set; }
			public DecoratedText[] Headers { get; private set; }
			public DecoratedText[] Texts { get; private set; }
			public int TextLength { get; private set; }
			public WeakReference ProfileImage { get; set; }

			public static string ToString (DecoratedText[] texts)
			{
				StringBuilder sb = new StringBuilder ();
				for (int i = 0; i < texts.Length; i ++)
					sb.Append (texts[i].ToString ());
				return sb.ToString ();
			}

			public override string ToString ()
			{
				return ToString (Texts);
			}

			public StatusBase Status { get; private set; }
			public IStatusRenderer Renderer {
				get { return TweetRenderer.Instance; }
			}
		}
		class CustomTextSource : TextSource
		{
			DecoratedText[] _texts;
			string _rawText = null;

			public CustomTextSource (DecoratedText[] texts)
			{
				_texts = texts;
			}

			public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText (int textSourceCharacterIndexLimit)
			{
				if (_rawText == null)
					_rawText = FormattedText.ToString (_texts);
				CharacterBufferRange cbr = new CharacterBufferRange (_rawText, 0, textSourceCharacterIndexLimit);
				return new TextSpan<CultureSpecificCharacterBufferRange> (
					textSourceCharacterIndexLimit,
					new CultureSpecificCharacterBufferRange (CultureInfo.CurrentUICulture, cbr));
			}

			public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex (int textSourceCharacterIndex)
			{
				throw new NotImplementedException ();
			}

			public override TextRun GetTextRun (int textSourceCharacterIndex)
			{
				int pos = 0;
				for (int i = 0; i <= _texts.Length; i ++) {
					if (i < _texts.Length && pos == textSourceCharacterIndex) {
						DecoratedText t = _texts[i];
						return new TextCharacters (t.Text, t.Offset, t.Length, t.Properties);
					} else if (pos > textSourceCharacterIndex) {
						DecoratedText t = _texts[i - 1];
						string text = t.ToString ();
						int offset = textSourceCharacterIndex - (pos - text.Length);
						TextCharacters c = new TextCharacters (text, offset, text.Length - offset, t.Properties);
						return c;
					}
					if (i < _texts.Length)
						pos += _texts[i].Length;
				}
				return new TextEndOfParagraph (1);
			}
		}
		#endregion
	}
}
