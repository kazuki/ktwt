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

namespace ktwt.Twitter
{
	public class TwitterClient
	{
		public static Uri RequestTokenURL = new Uri ("https://twitter.com/oauth/request_token");
		public static Uri AccessTokenURL = new Uri ("https://twitter.com/oauth/access_token");
		public static Uri AuthorizeURL = new Uri ("https://twitter.com/oauth/authorize");

		const string X_RateLimit_Limit = "X-RateLimit-Limit";
		const string X_RateLimit_Remaining = "X-RateLimit-Remaining";
		const string X_RateLimit_Reset = "X-RateLimit-Reset";
		static DateTime UnixTimeStart = new DateTime (1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		const string HTTP_GET = "GET";
		const string HTTP_POST = "POST";
		const string UrlEncodedMime = "application/x-www-form-urlencoded";

		ISimpleWebClient _client;

		int _apiLimitMax = -1, _apiLimitRemaining = -1;
		DateTime _apiLimitResetTime = DateTime.MaxValue;

		public event EventHandler ApiLimitChanged;

		public TwitterClient (ISimpleWebClient baseClient)
		{
			_client = baseClient;
		}

		#region API Info
		public int ApiLimitMax {
			get { return _apiLimitMax; }
		}

		public int ApiLimitRemaining {
			get { return _apiLimitRemaining; }
		}

		public DateTime ApiLimitResetTime {
			get { return _apiLimitResetTime; }
		}
		#endregion

		#region Streaming API
		public IStreamingState StartStreaming (Uri uri, string method, string postData)
		{
			return StartStreaming (uri, method, postData, _client.Credentials);
		}

		public IStreamingState StartStreaming (Uri uri, string method, string postData, ICredentials credentials)
		{
			HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create (uri);
			req.Credentials = credentials;
			req.Method = method;
			if (method == HTTP_POST && postData != null && postData.Length > 0) {
				byte[] rawData = Encoding.ASCII.GetBytes (postData);
				req.ContentLength = rawData.Length;
				Stream strm = req.GetRequestStream ();
				strm.Write (rawData, 0, rawData.Length);
				req.ContentType = UrlEncodedMime;
			}

			HttpWebResponse res = (HttpWebResponse)req.GetResponse ();
			return new StreamingState (res);
		}

		public void StopStreaming (IStreamingState state)
		{
			StreamingState ss = state as StreamingState;
			if (ss == null)
				throw new ArgumentException ();
			ss.Dispose ();
		}

		class StreamingState : IStreamingState, IDisposable
		{
			HttpWebResponse _res;
			Stream _strm;
			bool _closed = false;

			public StreamingState (HttpWebResponse res)
			{
				_res = res;
				_strm = res.GetResponseStream ();
			}

			public HttpWebResponse Response {
				get { return _res; }
			}

			public Stream Stream {
				get { return _strm; }
			}

			public bool IsClosed {
				get { return _closed; }
			}

			public void Dispose ()
			{
				lock (this) {
					if (_closed)
						return;
					_closed = true;
				}
				try {
					_strm.Dispose ();
				} catch {}
				try {
					_res.Close ();
				} catch {}
			}
		}
		#endregion

		#region Misc
		public string DownloadString (Uri uri, string method, byte[] postData)
		{
			WebHeaderCollection headers;
			return DownloadString (uri, method, postData, out headers);
		}

		string DownloadString (Uri uri, string method, byte[] postData, out WebHeaderCollection headers)
		{
			string text = _client.DownloadString (uri, method, postData, out headers);

			long temp;
			string value = headers[X_RateLimit_Limit];
			if (value != null && long.TryParse (value, out temp)) _apiLimitMax = (int)temp;
			value = headers[X_RateLimit_Remaining];
			if (value != null && long.TryParse (value, out temp)) _apiLimitRemaining = (int)temp;
			value = headers[X_RateLimit_Reset];
			if (value != null && long.TryParse (value, out temp)) _apiLimitResetTime = UnixTimeStart + TimeSpan.FromSeconds (temp);

			if (ApiLimitChanged != null) {
				try {
					ApiLimitChanged.BeginInvoke (this, EventArgs.Empty, null, null);
				} catch {}
			}

			return text;
		}
		#endregion
	}
}
