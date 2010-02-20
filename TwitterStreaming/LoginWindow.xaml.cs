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
using System.Windows;

namespace TwitterStreaming
{
	public partial class LoginWindow : Window
	{
		public LoginWindow ()
		{
			InitializeComponent ();
			Initialized += new EventHandler (LoginWindow_Initialized);
		}

		void LoginWindow_Initialized (object sender, EventArgs e)
		{
			txtUsername.SelectAll ();
			txtPassword.SelectAll ();
			txtUsername.Focus ();
		}

		private void LoginButton_Click (object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close ();
		}

		private void CancelButton_Click (object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close ();
		}

		public string UserName {
			get { return txtUsername.Text; }
			set { txtUsername.Text = value;}
		}

		public string Password {
			get { return txtPassword.Password; }
			set { txtPassword.Password = value; }
		}

		public bool UseTrackMode {
			get { return chkTrack.IsChecked.Value; }
			set { chkTrack.IsChecked = value; }
		}

		public string TrackWords {
			get { return txtTrackWords.Text; }
			set { txtTrackWords.Text = value;}
		}
	}
}
