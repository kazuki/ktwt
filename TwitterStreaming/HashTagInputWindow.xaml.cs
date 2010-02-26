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

namespace TwitterStreaming
{
	public partial class HashTagInputWindow : Window
	{
		public HashTagInputWindow ()
		{
			InitializeComponent ();
		}

		private void Button_Click (object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			this.Close ();
		}

		private void CancelButton_Click (object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close ();
		}

		public string HashTagText {
			get { return txtHashTag.Text; }
		}
	}
}
