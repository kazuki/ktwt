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

		TweetRenderer ()
		{
			FontFamily ff = SystemFonts.MessageFontFamily;
			Typeface tf = new Typeface (ff, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			double fontSize = 10.5;
			CultureInfo ci = CultureInfo.CurrentUICulture;

			DefaultTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.Black, fontSize, fontSize, null, null, ci);
			UrlTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.Blue, fontSize, fontSize, TextDecorations.Underline, null, ci);
			UserNameTextRunProperties = new BasicTextRunProperties (tf, null, Brushes.Red, fontSize, fontSize, TextDecorations.Underline, null, ci);
			HashTagRunProperties = new BasicTextRunProperties (tf, null, Brushes.Green, fontSize, fontSize, TextDecorations.Underline, null, ci);

			ParagraphProperties = new BasicTextParagraphProperties (DefaultTextRunProperties,
				false, FlowDirection.LeftToRight, 0.0, 0.0, TextAlignment.Left, null, TextWrapping.Wrap);
		}

		public IDecoratedStatus Decorate (StatusBase status)
		{
			string text = status.Text;
			Match m = TweetRegex.Match (text);
			int last = 0;
			List<DecoratedText> inlines = new List<DecoratedText> ();
			while (m.Success) {
				if (m.Index > last)
					inlines.Add (new DecoratedText (DefaultTextRunProperties, text, last, m.Index - last, false));
				if (m.Success) {
					TextRunProperties prop = null;
					if (m.Groups[TweetRegetUrlGroupName].Success) {
						prop = UrlTextRunProperties;
					} else if (m.Groups[TweetRegetUserNameGroupName].Success) {
						prop = UserNameTextRunProperties;;
					} else if (m.Groups[TweetRegetHashTagGroupName].Success) {
						prop = HashTagRunProperties;
					}
					if (prop == null)
						prop = DefaultTextRunProperties;
					
					Capture c = m.Captures[0];
					inlines.Add (new DecoratedText (prop, text, c.Index, c.Length, false));
				}

				last = m.Index + m.Length;
				m = m.NextMatch ();
			}
			if (last < text.Length)
				inlines.Add (new DecoratedText (DefaultTextRunProperties, text, last, false));
			return new FormattedText (status, ParagraphProperties, inlines.ToArray ());
		}

		public TextRunProperties DefaultTextRunProperties { get; set; }
		public TextRunProperties UrlTextRunProperties { get; set; }
		public TextRunProperties UserNameTextRunProperties { get; set; }
		public TextRunProperties HashTagRunProperties { get; set; }
		public TextParagraphProperties ParagraphProperties { get; set; }

		public double Render (TextFormatter formatter, DrawingContext drawingContext, IDecoratedStatus item, double offset_y, double width)
		{
			FormattedText text = (FormattedText)item;
			Status s = (Status)item.Status;
			User u = s.User as User;

			double padding_left = 5;
			double padding_top = 2;
			double padding_right = 5;
			double x = padding_left;
			double y = offset_y + padding_top;
			int pos = 0;
			CustomTextSource source = new CustomTextSource (text);

			// draw profile image

			// draw header (todo)

			// draw text
			x = padding_left;
			double text_width = width - x - padding_right;
			while (pos < item.Status.Text.Length) {
				using (TextLine line = formatter.FormatLine (source, pos, text_width, text.ParagraphProperties, null)) {
					line.Draw (drawingContext, new Point (x, y), InvertAxes.None);
					pos += line.Length;
					y += line.Height;
				}
			}

			return y - offset_y;
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
			public FormattedText (StatusBase status, TextParagraphProperties prop, DecoratedText[] texts)
			{
				Status = status;
				ParagraphProperties = prop;
				Texts = texts;
			}

			public TextParagraphProperties ParagraphProperties { get; set; }
			public DecoratedText[] Texts { get; private set; }

			public override string ToString ()
			{
				StringBuilder sb = new StringBuilder ();
				for (int i = 0; i < Texts.Length; i ++)
					sb.Append (Texts[i].ToString ());
				return sb.ToString ();
			}

			public StatusBase Status { get; private set; }
			public IStatusRenderer Renderer {
				get { return TweetRenderer.Instance; }
			}
		}
		class CustomTextSource : TextSource
		{
			FormattedText _text;
			string _rawText = null;

			public CustomTextSource (FormattedText text)
			{
				_text = text;
			}

			public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText (int textSourceCharacterIndexLimit)
			{
				if (_rawText == null)
					_rawText = _text.ToString ();
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
				for (int i = 0; i <= _text.Texts.Length; i ++) {
					if (i < _text.Texts.Length && pos == textSourceCharacterIndex) {
						DecoratedText t = _text.Texts[i];
						return new TextCharacters (t.Text, t.Offset, t.Length, t.Properties);
					} else if (pos > textSourceCharacterIndex) {
						DecoratedText t = _text.Texts[i - 1];
						string text = t.ToString ();
						int offset = textSourceCharacterIndex - (pos - text.Length);
						TextCharacters c = new TextCharacters (text, offset, text.Length - offset, t.Properties);
						return c;
					}
					if (i < _text.Texts.Length)
						pos += _text.Texts[i].Length;
				}
				return new TextEndOfParagraph (1);
			}
		}
		#endregion
	}
}
