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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ktwt.Json;

namespace ktwt.Twitter
{
	public class Status
	{
		public Status ()
		{
		}

		public Status (JsonObject status)
		{
			JsonObject user = (JsonObject)status.Value["user"];
			Text = (status.Value["text"] as JsonString).Value.Replace ("&lt;", "<").Replace ("&gt;", ">").Replace ("&quot;", "\"").Replace ("&amp;", "&");
			ID = (ulong)(status.Value["id"] as JsonNumber).Value;
			UserID = (ulong)(user.Value["id"] as JsonNumber).Value;
			Name = (user.Value["name"] as JsonString).Value;
			ScreenName = (user.Value["screen_name"] as JsonString).Value;
			ProfileImageUrl = (user.Value["profile_image_url"] as JsonString).Value;
		}

		public ulong ID { get; set; }
		public ulong UserID { get; set; }
		public string ScreenName { get; set; }
		public string Name { get; set; }
		public string Text { get; set; }
		public string ProfileImageUrl { get; set; }
		public ImageSource ProfileImage { get; set; }

		public void TrySetProfileImage (Dictionary<string, ImageSource> imgCache)
		{
			ImageSource source;
			imgCache.TryGetValue (ProfileImageUrl, out source);
			if (source == null) {
				source = new BitmapImage (new Uri (ProfileImageUrl));
				imgCache.Add (ProfileImageUrl, source);
				ProfileImage = source;
			}
			ProfileImage = source;
		}
	}
}
