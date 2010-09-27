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
using System.Windows.Media.TextFormatting;

namespace ktwt.ui
{
	class BasicTextParagraphProperties : TextParagraphProperties
	{
		TextRunProperties _defaultTextRunProperties;
		bool _firstLineInParagraph;
		FlowDirection _flowDirection;
		double _indent, _lineHeight;
		TextAlignment _textAlignment;
		TextMarkerProperties _textMarkerProperties;
		TextWrapping _textWrapping;

		public BasicTextParagraphProperties (
			TextRunProperties defaultTextRunProperties,
			bool firstLineInParagraph,
			FlowDirection flowDirection,
			double indent, double lineHeight,
			TextAlignment textAlignment,
			TextMarkerProperties textMarkerProperties,
			TextWrapping textWrapping
			)
		{
			_defaultTextRunProperties = defaultTextRunProperties;
			_firstLineInParagraph = firstLineInParagraph;
			_flowDirection = flowDirection;
			_indent = indent;
			_lineHeight = lineHeight;
			_textAlignment = textAlignment;
			_textMarkerProperties = textMarkerProperties;
			_textWrapping = textWrapping;
		}

		public override TextRunProperties DefaultTextRunProperties {
			get { return _defaultTextRunProperties; }
		}

		public override bool FirstLineInParagraph {
			get { return _firstLineInParagraph; }
		}

		public override FlowDirection FlowDirection {
			get { return _flowDirection; }
		}

		public override double Indent {
			get { return _indent; }
		}

		public override double LineHeight {
			get { return _lineHeight; }
		}

		public override TextAlignment TextAlignment {
			get { return _textAlignment; }
		}

		public override TextMarkerProperties TextMarkerProperties {
			get { return _textMarkerProperties; }
		}

		public override TextWrapping TextWrapping {
			get { return _textWrapping; }
		}
	}
}
