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
using ktwt.Json;
using ktwt.StatusStream;

namespace ktwt.Twitter
{
	public class Status : StatusBase, INotifyPropertyChanged
	{
		string _text = string.Empty;

		public Status ()
		{
		}

		[JsonObjectMapping ("created_at", JsonValueType.String)]
		DateTime CreatedAt_Internal {
			set { CreatedAt = value; }
		}

		[JsonObjectMapping ("id", JsonValueType.Number)]
		public ulong ID { get; set; }

		[JsonObjectMapping ("text", JsonValueType.String)]
		string Text_Internal {
			set { Text = value.Replace ("&lt;", "<").Replace ("&gt;", ">").Replace ("&quot;", "\"").Replace ("&amp;", "&"); }
		}

		[JsonObjectMapping ("source", JsonValueType.String)]
		public string Source { get; set; }

		[JsonObjectMapping ("truncated", JsonValueType.Boolean)]
		public bool IsTruncated { get; set; }

		[JsonObjectMapping ("in_reply_to_status_id", JsonValueType.Number)]
		public ulong InReplyToStatusId { get; set; }

		[JsonObjectMapping ("in_reply_to_user_id", JsonValueType.Number)]
		public ulong InReplyToUserId { get; set; }

		[JsonObjectMapping ("in_reply_to_screen_name", JsonValueType.String)]
		public string InReplyToScreenName { get; set; }

		bool _isFav;
		[JsonObjectMapping ("favorited", JsonValueType.Boolean)]
		public bool IsFavorited {
			get { return _isFav; }
			set {
				_isFav = value;
				if (PropertyChanged != null) {
					try {
						PropertyChanged (this, new PropertyChangedEventArgs ("IsFavorited"));
					} catch {}
				}
			}
		}

		[JsonObjectMapping ("user", JsonValueType.Object)]
		User User_Internal {
			set { User = value; }
		}

		[JsonObjectMapping ("retweeted_status", JsonValueType.Object)]
		public Status RetweetedStatus { get; set; }

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
