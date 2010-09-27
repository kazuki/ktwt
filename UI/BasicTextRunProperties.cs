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

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;

namespace ktwt.ui
{
	class BasicTextRunProperties : TextRunProperties
	{
		Typeface _typeface;
		double _emSize, _emHintingSize;
		TextDecorationCollection _textDecorations;
		TextEffectCollection _textEffects;
		Brush _foregroundBrush, _backgroundBrush;
		CultureInfo _culture;

		public BasicTextRunProperties (
			Typeface typeface, Brush bgBrush, Brush fgBrush,
			double renderingEmSize, double hintingEmSize,
			TextDecorationCollection decoration, TextEffectCollection effects, CultureInfo culture)
		{
			_typeface = typeface;
			_foregroundBrush = fgBrush;
			_backgroundBrush = bgBrush;
			_emSize = renderingEmSize;
			_emHintingSize = hintingEmSize;
			_textDecorations = decoration;
			_textEffects = effects;
			_culture = culture;
		}

		public override Typeface Typeface {
			get { return _typeface; }
		}

		public override double FontRenderingEmSize {
			get { return _emSize; }
		}

		public override double FontHintingEmSize {
			get { return _emHintingSize; }
		}

		public override TextDecorationCollection TextDecorations {
			get { return _textDecorations; }
		}

		public override Brush BackgroundBrush {
			get { return _backgroundBrush; }
		}

		public override Brush ForegroundBrush {
			get { return _foregroundBrush; }
		}

		public override CultureInfo CultureInfo {
			get { return _culture; }
		}

		public override TextEffectCollection TextEffects {
			get { return _textEffects; }
		}
	}
}
