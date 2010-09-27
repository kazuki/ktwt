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
using ktwt.Threading;
using ktwt.Twitter;

namespace ktwt.ui
{
	public partial class MainWindow : Window
	{
		IntervalTimer _timer;
		TwitterAccountNode _node;

		public MainWindow ()
		{
			InitializeComponent ();

			_timer = new IntervalTimer (TimeSpan.FromSeconds (0.5), Environment.ProcessorCount);
			_node = new TwitterAccountNode (_timer);
			_node.OAuthClient.PasswordAuth ("username", "password");
			//viewer.AddInputStream (_node.AddUserStream ());
			//viewer.AddInputStream (_node.AddRestStream (new RestUsage {Type=RestType.Home, Count=300, Interval=TimeSpan.FromSeconds (60), IsEnabled=true}));
			viewer.AddInputStream (_node.AddSampleStream ());
		}

		protected override void OnClosed (EventArgs e)
		{
			_timer.Dispose ();
			_node.Dispose ();
			base.OnClosed (e);
		}
	}
}
