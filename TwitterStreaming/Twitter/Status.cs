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

using ktwt.Json;

namespace ktwt.Twitter
{
	public class Status
	{
		public Status ()
		{
		}

		[JsonObjectMapping ("id", JsonValueType.Number)]
		public ulong ID { get; set; }

		[JsonObjectMapping ("text", JsonValueType.String)]
		public string Text { get; set; }

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

		[JsonObjectMapping ("favorited", JsonValueType.Boolean)]
		public bool IsFavorited { get; set; }

		[JsonObjectMapping ("user", JsonValueType.Object)]
		public User User { get; set; }
	}
}
