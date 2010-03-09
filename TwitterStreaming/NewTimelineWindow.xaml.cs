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
using System.Windows.Controls;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public partial class NewTimelineWindow : Window
	{
		TwitterAccountManager _mgr;
		bool _initialized = false;

		public NewTimelineWindow (TwitterAccountManager mgr)
		{
			_mgr = mgr;
			DataContext = mgr;
			Initialized += delegate (object o, EventArgs e) {
				_initialized = true;
				Validate ();
			};
			InitializeComponent ();
		}

		public bool IsCheckedAccountTimeline {
			get { return chk0.IsChecked.HasValue && chk0.IsChecked.Value; }
		}

		public bool IsCheckedNewSearch {
			get { return chk1.IsChecked.HasValue && chk1.IsChecked.Value; }
		}

		public bool IsCheckedExistedSearch {
			get { return chk2.IsChecked.HasValue && chk2.IsChecked.Value; }
		}

		public bool IsCheckedNewTab {
			get { return chk3.IsChecked.HasValue && chk3.IsChecked.Value; }
		}

		public bool IsCheckedList {
			get { return chk4.IsChecked.HasValue && chk4.IsChecked.Value; }
		}

		public bool IsUseStreamingForSearch {
			get { return searchStreaming.IsChecked.HasValue && searchStreaming.IsChecked.Value; }
		}

		public bool IsUseStreamingForList {
			get { return listStreaming.IsChecked.HasValue && listStreaming.IsChecked.Value; }
		}

		public TwitterAccount SelectedAccount {
			get {
				if (IsCheckedAccountTimeline)
					return tlAccount.SelectedItem as TwitterAccount;
				if (IsCheckedNewSearch)
					return searchAccount.SelectedItem as TwitterAccount;
				if (IsCheckedList)
					return listAccount.SelectedItem as TwitterAccount;
				return null;
			}
		}

		public ListInfo SelectedList {
			get { return listList.SelectedItem as ListInfo; }
		}

		public TwitterAccount SelectedListStreamingAccount {
			get {
				if (!IsUseStreamingForList)
					return null;
				return listStreamingAccount.SelectedItem as TwitterAccount;
			}
		}
		
		public TwitterTimeLine SelectedAccountTimeline {
			get {
				TwitterAccount account = tlAccount.SelectedItem as TwitterAccount;
				if (account == null)
					return null;

				switch (tlType.SelectedIndex) {
					case 0:
						return account.HomeTimeline;
					case 1:
						return account.Mentions;
					case 2:
						return account.DirectMessages;
					default:
						return null;
				}
			}
		}

		public SearchStatuses SelectedExistedSearch {
			get { return existSearches.SelectedItem as SearchStatuses; }
		}

		public string SearchKeyword {
			get { return searchText.Text.Trim (); }
		}

		public string NewTabTitle {
			get { return tabTitle.Text.Trim (); }
		}

		private void AddButton_Click (object sender, RoutedEventArgs e)
		{
			DialogResult =	true;
		}

		private void CancelButton_Click (object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close ();
		}

		private void RadioButton_Checked (object sender, RoutedEventArgs e)
		{
			Validate ();
		}

		private void ComboBox_SelectionChanged (object sender, SelectionChangedEventArgs e)
		{
			Validate ();
		}

		private void TextBox_TextChanged (object sender, TextChangedEventArgs e)
		{
			Validate ();
		}

		void Validate ()
		{
			if (!_initialized)
				return;

			bool isValid = true;
			try {
				if (IsCheckedAccountTimeline) {
					isValid = (SelectedAccountTimeline != null);
				} else if (IsCheckedNewSearch) {
					isValid = (SelectedAccount != null);
					isValid &= (SearchKeyword.Length > 0);
				} else if (IsCheckedExistedSearch) {
					isValid = (SelectedExistedSearch != null);
				} else if (IsCheckedNewTab) {
					isValid = (NewTabTitle.Length > 0);
				} else if (IsCheckedList) {
					isValid = (SelectedAccount != null);
					isValid &= (SelectedList != null);
					if (IsUseStreamingForList)
						isValid &= (SelectedListStreamingAccount != null);
				} else {
					isValid = false;
				}
			} catch {
				isValid = false;
			}
			AddButton.IsEnabled = isValid;
		}

		private void searchStreaming_Checked (object sender, RoutedEventArgs e)
		{
			if (IsUseStreamingForSearch) {
				List<TwitterAccount> list = new List<TwitterAccount> ();
				for (int i = 0; i < _mgr.Accounts.Length; i ++)
					if (_mgr.Accounts[i].StreamingClient == null)
						list.Add (_mgr.Accounts[i]);
				searchAccount.ItemsSource = list.ToArray ();
			} else {
				searchAccount.ItemsSource = _mgr.Accounts;
			}
			Validate ();
		}

		private void listStreaming_Checked (object sender, RoutedEventArgs e)
		{
			if (IsUseStreamingForList) {
				List<TwitterAccount> list = new List<TwitterAccount> ();
				for (int i = 0; i < _mgr.Accounts.Length; i++)
					if (_mgr.Accounts[i].StreamingClient == null)
						list.Add (_mgr.Accounts[i]);
				listStreamingAccount.ItemsSource = list.ToArray ();
				listStreamingAccount.IsEnabled = true;
			} else {
				listStreamingAccount.ItemsSource = _mgr.Accounts;
				listStreamingAccount.IsEnabled = false;
			}
			Validate ();
		}
	}
}
