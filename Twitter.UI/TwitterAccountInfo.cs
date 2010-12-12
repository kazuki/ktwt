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

using ktwt.ui;

namespace ktwt.Twitter.ui
{
	class TwitterAccountInfo : IAccountInfo
	{
		public TwitterAccountInfo (TwitterOAuthCredentialCache credential)
		{
			Credential = credential;
			Summary = string.Format ("{0} (id={1})", credential.ScreenName, credential.UserID);
		}

		public string Summary { get; private set; }
		public TwitterOAuthCredentialCache Credential { get; private set; }

		public IStatusSourceNodeInfo SourceNodeInfo {
			get { return TwitterNodeInfo.Instance; }
		}

		public bool Equals (IAccountInfo other)
		{
			TwitterAccountInfo info = other as TwitterAccountInfo;
			if (info == null)
				return false;
			return this.Credential.UserID == info.Credential.UserID;
		}
	}
}
