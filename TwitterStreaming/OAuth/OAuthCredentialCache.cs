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
using System.Net;

namespace ktwt.OAuth
{
	public class OAuthCredentialCache : ICredentials
	{
		public OAuthCredentialCache (string accessToken, string accessSecret)
		{
			AccessToken = accessToken;
			AccessSecret = accessSecret;
		}

		public string AccessToken { get; private set; }
		public string AccessSecret { get; private set; }

		public virtual NetworkCredential GetCredential (Uri uri, string authType)
		{
			throw new NotSupportedException ();
		}
	}
}
