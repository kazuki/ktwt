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
using System.IO;
using System.Net;
using System.Text;
using ktwt.OAuth;

namespace TwitterStreaming
{
	public class UrlShortener
	{
		static HttpWebRequest CreateRequest (UrlShortenerServices service, string url)
		{
			string requestUrl = null;
			switch (service) {
				case UrlShortenerServices.tinyURL:
					requestUrl = "http://tinyurl.com/api-create.php?url=" + OAuthBase.UrlEncode (url);
					break;
				case UrlShortenerServices.toly:
					requestUrl = "http://to.ly/api.php?longurl=" + OAuthBase.UrlEncode (url);
					break;
				default:
					throw new ArgumentException ();
			}
			return (HttpWebRequest)WebRequest.Create (requestUrl);
		}

		static Uri ParseResponse (UrlShortenerServices service, string responseBody)
		{
			switch (service) {
				case UrlShortenerServices.tinyURL:
				case UrlShortenerServices.toly:
					return new Uri (responseBody);
				default:
					throw new ArgumentException ();
			}
		}

		public static Uri Shortener (UrlShortenerServices service, string url)
		{
			HttpWebRequest req = CreateRequest (service, url);
			using (HttpWebResponse res = (HttpWebResponse)req.GetResponse ())
			using (Stream strm = res.GetResponseStream ())
			using (StreamReader reader = new StreamReader (strm, Encoding.ASCII)) {
				 return ParseResponse (service, reader.ReadToEnd ().Trim ());
			}
		}

		public static Uri Shortener (UrlShortenerServices service, Uri url)
		{
			return Shortener (service, url.ToString ());
		}
	}

	public enum UrlShortenerServices
	{
		tinyURL,
		toly
	}
}
