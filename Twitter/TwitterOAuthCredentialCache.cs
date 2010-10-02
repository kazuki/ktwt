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

using ktwt.OAuth;
using ktwt.Json;

namespace ktwt.Twitter
{
	public class TwitterOAuthCredentialCache : OAuthCredentialCache
	{
		public TwitterOAuthCredentialCache (ulong userId, string screenName, OAuthCredentialCache cache)
			: this (userId, screenName, cache.AccessToken, cache.AccessSecret)
		{
		}

		public TwitterOAuthCredentialCache (ulong userId, string screenName, string accessToken, string accessSecret)
			: base (accessToken, accessSecret)
		{
			UserID = userId;
			ScreenName = screenName;
		}

		[JsonObjectMapping ("user_id", JsonValueType.Number)]
		public ulong UserID { get; set; }

		[JsonObjectMapping ("screen_name", JsonValueType.String)]
		public string ScreenName { get; set; }

		[JsonObjectMapping ("token", JsonValueType.String)]
		string AccessToken_Internal {
			get { return AccessToken; }
			set { AccessToken = value; }
		}

		[JsonObjectMapping ("secret", JsonValueType.String)]
		string AccessSecret_Internal {
			get { return AccessSecret; }
			set { AccessSecret = value; }
		}
	}
}
