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
	partial class MainWindow : Window
	{
		IntervalTimer _timer;
		TwitterAccountNode[] _nodes;
		Configurations _config;

		public MainWindow (Configurations config)
		{
			InitializeComponent ();

			_config = config;
			_timer = new IntervalTimer (TimeSpan.FromSeconds (0.5), Environment.ProcessorCount);
			_nodes = new TwitterAccountNode[_config.TwitterAccounts.Length];
			for (int i = 0; i < _config.TwitterAccounts.Length; i ++) {
				_nodes[i] = new TwitterAccountNode (_timer);
				_nodes[i].Credential = _config.TwitterAccounts[i];

				viewer.AddInputStream (_nodes[i].AddUserStream ());
				viewer.AddInputStream (_nodes[i].AddRestStream (new RestUsage {Type=RestType.Home, Count=300, Interval=TimeSpan.FromSeconds (60), IsEnabled=true}));
			}
		}

		protected override void OnClosed (EventArgs e)
		{
			_timer.Dispose ();
			for (int i = 0; i < _nodes.Length; i ++)
				_nodes[i].Dispose ();
			base.OnClosed (e);
		}
	}
}
