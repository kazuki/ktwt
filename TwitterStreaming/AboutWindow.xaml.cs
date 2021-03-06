﻿/*
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
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TwitterStreaming
{
	public partial class AboutWindow : Window, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		public AboutWindow ()
		{
			Assembly asm = Assembly.GetExecutingAssembly ();
			AssemblyTitle = ((AssemblyTitleAttribute)Attribute.GetCustomAttribute (asm, typeof (AssemblyTitleAttribute))).Title;
			Copyright = ((AssemblyCopyrightAttribute)Attribute.GetCustomAttribute (asm, typeof (AssemblyCopyrightAttribute))).Copyright;
			Description = ((AssemblyDescriptionAttribute)Attribute.GetCustomAttribute (asm, typeof (AssemblyDescriptionAttribute))).Description;
			Version = asm.GetName ().Version;
			InitializeComponent ();
			LoadLicense (licenseLitJSON, asm, "TwitterStreaming.Json.LitJSON.COPYING.txt");
			LoadLicense (licenseOAuthBase, asm, "TwitterStreaming.COPYING.APACHE_LICENSE-2.0.txt");
			LoadLicense (licenseGPL, asm, "TwitterStreaming.COPYING.GPL.txt");

			DispatcherTimer timer = new DispatcherTimer ();
			timer.Interval = TimeSpan.FromSeconds (1);
			timer.Tick += delegate (object sender, EventArgs e) {
				InvokePropertyChanged ("TotalManagedMemory");
				InvokePropertyChanged ("IconCacheEntries");
			};
			timer.Start ();
		}

		public string AssemblyTitle { get; set; }
		public string Copyright { get; set; }
		public string Description { get; set; }
		public Version Version { get; set; }
		public string TotalManagedMemory {
			get { return (GC.GetTotalMemory (false) / 1024.0 / 1024.0).ToString ("f2") + " MB"; }
		}
		public string IconCacheEntries {
			get { return string.Format ("Friends:{0} / Other:{1}",
				IconCache.FriendsCache.Count, IconCache.NonFriendCache.Count); }
		}

		static void LoadLicense (TextBox block, Assembly asm, string name)
		{
			using (Stream strm = asm.GetManifestResourceStream (name)) {
				string txt = new StreamReader (strm).ReadToEnd ();
				block.Text = txt;
			}
			block.FontSize = 11;
			block.FontFamily = new FontFamily ("Global Monospace");
		}

		private void Button_Click (object sender, RoutedEventArgs e)
		{
			Close ();
		}

		private void Force_GC_Button_Click (object sender, RoutedEventArgs e)
		{
			GC.Collect ();
		}

		private void ClearFriendIcon_Click (object sender, RoutedEventArgs e)
		{
			IconCache.FriendsCache.Clear ();
		}

		private void ClearOtherIcon_Click (object sender, RoutedEventArgs e)
		{
			IconCache.NonFriendCache.Clear ();
		}

		void InvokePropertyChanged (string name)
		{
			if (PropertyChanged != null)
				PropertyChanged (this, new PropertyChangedEventArgs (name));
		}
	}
}
