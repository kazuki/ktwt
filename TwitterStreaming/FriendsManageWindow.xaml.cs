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
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class FriendsManageWindow : Window
	{
		public FriendsManageWindow (TwitterAccountManager mgr)
		{
			InitializeComponent ();
			DataContext = mgr;
		}

		private void TextBox_TextChanged (object sender, TextChangedEventArgs e)
		{
			TextBox tb = sender as TextBox;
			string keyword = tb.Text.ToLower ();
			TwitterAccount account = (TwitterAccount)tb.DataContext;
			ICollectionView view1 = CollectionViewSource.GetDefaultView (account.TwitterClient.Friends);
			ICollectionView view2 = CollectionViewSource.GetDefaultView (account.TwitterClient.Followers);
			view1.Filter = view2.Filter = delegate (object item) {
				User user = (User)item;
				return 
					user.Name.ToLower ().Contains (keyword) ||
					user.ScreenName.ToLower ().Contains (keyword) ||
					(user.Description != null && user.Description.ToLower ().Contains (keyword));
			};
		}
	}

	[ValueConversion (typeof (string), typeof (string))]
	public class RemoveCrLfConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is string)
				return (value as string).Replace ("\r", "").Replace ("\n", "");
			return value;
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return value;
		}
	}

	[ValueConversion (typeof (User[]), typeof (ICollectionView))]
	public class UserArrayConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return new ListCollectionView ((User[])value);
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return value;
		}
	}
}
