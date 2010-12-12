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

using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace ktwt.ui
{
	public partial class OptionWindow : Window
	{
		public OptionWindow (Configurations config)
		{
			Config = config.Clone ();
			InitializeComponent ();
			btnOK.SizeChanged += delegate (object sender, SizeChangedEventArgs e) {
				OverlapMargin = new Thickness (5, 5, 5, 10.0 + btnOK.ActualHeight);
			};
			if (AccountTypes.Count > 0)
				account_type.SelectedIndex = 0;
		}

		public Configurations Config { get; set; }
		public IList<string> AccountTypes { get { return StatusTypes.SourceTypes; } }

		#region Account

		private void  Account_Add_Click (object sender, RoutedEventArgs e)
		{
			IStatusSourceNodeInfo info = StatusTypes.GetInfo ((string)account_type.SelectedItem);
			IAccountInfo account = info.CreateAccountWithGUI (this);
			if (account == null)
				return;

			List<IAccountInfo> list = new List<IAccountInfo> ();
			if (Config.Accounts != null)
				list.AddRange (Config.Accounts);
			for (int i = 0; i < list.Count; i ++) {
				if (account.Equals (list[i])) {
					MessageBox.Show ("既に登録されているアカウントです", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}
			}
			list.Add (account);
			Config.Accounts = list.ToArray ();
			UpdateBindingTarget (this, "Config.Accounts");
			Config.Save ();
		}

		private void Account_Remove_Click (object sender, RoutedEventArgs e)
		{
			MessageBox.Show ("Not Implemented");
		}

		private void Account_ReAuth_Click (object sender, RoutedEventArgs e)
		{
			MessageBox.Show ("Not Implemented");
		}

		#endregion

		#region Misc
		private void UpdateBindingTarget (object source, string path)
		{
			for (int i = 0; i < BindingGroup.BindingExpressions.Count; i ++) {
				BindingExpression exp = BindingGroup.BindingExpressions[i] as BindingExpression;
				if (exp == null) continue;
				if (exp.DataItem != source || exp.ParentBinding == null || exp.ParentBinding.Path == null) continue;
				if (path.Equals (exp.ParentBinding.Path.Path))
					exp.UpdateTarget ();
			}
		}
		#endregion

		#region Dependency Properties
		public static readonly DependencyProperty OverlapMarginProperty =
			DependencyProperty.Register ("OverlapMargin", typeof (Thickness), typeof (OptionWindow), new FrameworkPropertyMetadata (new Thickness (5), FrameworkPropertyMetadataOptions.AffectsMeasure, null));
		public Thickness OverlapMargin {
			get { return (Thickness)GetValue (OverlapMarginProperty); }
			set { SetValue (OverlapMarginProperty, value); }
		}
		#endregion

		private void OK_Click (object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void Cancel_Click (object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}
	}
}
