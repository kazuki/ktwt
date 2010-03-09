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

using ktwt.Json;

namespace ktwt.Twitter
{
	public class ListInfo
	{
		public ListInfo ()
		{
		}

		[JsonObjectMapping ("id", JsonValueType.Number)]
		public ulong ID { get; set; }

		[JsonObjectMapping ("name", JsonValueType.String)]
		public string Name { get; set; }

		[JsonObjectMapping ("full_name", JsonValueType.String)]
		public string FullName { get; set; }

		[JsonObjectMapping ("slug", JsonValueType.String)]
		public string Slug { get; set; }

		[JsonObjectMapping ("description", JsonValueType.String)]
		public string Description { get; set; }

		[JsonObjectMapping ("subscriber_count", JsonValueType.Number)]
		public int SubscriberCount { get; set; }

		[JsonObjectMapping ("member_count", JsonValueType.Number)]
		public int MemberCount { get; set; }

		[JsonObjectMapping ("uri", JsonValueType.String)]
		public string Uri { get; set; }

		[JsonObjectMapping ("mode", JsonValueType.String)]
		public ListMode Mode { get; set; }

		[JsonObjectMapping ("user", JsonValueType.Object)]
		public User User { get; set; }
	}
}
