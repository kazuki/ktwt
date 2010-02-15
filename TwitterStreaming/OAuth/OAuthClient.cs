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
using System.IO;
using System.Net;
using System.Text;

namespace ktwt.OAuth
{
	public class OAuthClient : OAuthBase, ISimpleWebClient
	{
		string _consumerKey, _consumerSecret, _requestToken, _requestTokenSecret, _accessToken, _accessTokenSecret;
		Uri _requestTokenUri, _accessTokenUri, _authorizeUri;
		ICredentials _credentials;
		const string UrlEncodedMime = "application/x-www-form-urlencoded";

		public OAuthClient (string consumerKey, string consumerSecret, Uri requestTokenUri, Uri accessTokenUri, Uri authorizeUri)
		{
			_consumerKey = consumerKey;
			_consumerSecret = consumerSecret;
			_requestTokenUri = requestTokenUri;
			_accessTokenUri = accessTokenUri;
			_authorizeUri = authorizeUri;
		}

		public void UpdateRequestToken ()
		{
			WebClient client = new WebClient ();
			Dictionary<string, string> res = ParseSimple (client.DownloadString (_requestTokenUri));
			if (!res.ContainsKey (OAuthTokenKey) || !res.ContainsKey (OAuthTokenSecretKey))
				throw new Exception ();
			_requestToken = res[OAuthTokenKey];
			_requestTokenSecret = res[OAuthTokenSecretKey];
		}

		public Uri GetAuthorizeURL ()
		{
			if (_requestToken == null)
				UpdateRequestToken ();
			UriBuilder builder = new UriBuilder (_authorizeUri);
			builder.Query = OAuthTokenKey + "=" + _requestToken;
			return builder.Uri;
		}

		public void InputPIN (string pin)
		{
			Dictionary<string, string> contents;
			WebHeaderCollection headers;
			InputPIN (pin, out contents, out headers);
		}

		public void InputPIN (string pin, out Dictionary<string, string> contents, out WebHeaderCollection headers)
		{
			if (_requestTokenSecret == null)
				throw new Exception ();
			using (HttpWebResponse response = GetResponse (_accessTokenUri, HTTP_GET, _accessToken, _accessTokenSecret, pin, null, null)) {
				using (StreamReader reader = new StreamReader (response.GetResponseStream (), Encoding.ASCII)) {
					contents = ParseSimple (reader.ReadToEnd ());
				}
				headers = response.Headers;
			}
			if (!contents.ContainsKey (OAuthTokenKey) || !contents.ContainsKey (OAuthTokenSecretKey))
				throw new Exception ();
			_accessToken = contents[OAuthTokenKey];
			_accessTokenSecret = contents[OAuthTokenSecretKey];
		}

		public void PasswordAuth (string username, string password)
		{
			Dictionary<string, string> contents;
			WebHeaderCollection headers;
			PasswordAuth (username, password, out contents, out headers);
		}

		public void PasswordAuth (string username, string password, out Dictionary<string, string> contents, out WebHeaderCollection headers)
		{
			string xAuthQuery = "x_auth_mode=client_auth&x_auth_username=" + UrlEncode (username) + "&x_auth_password=" + UrlEncode (password);
			using (HttpWebResponse response = GetResponse (_accessTokenUri, HTTP_GET, null, null, null, xAuthQuery, null)) {
				using (StreamReader reader = new StreamReader (response.GetResponseStream (), Encoding.ASCII)) {
					contents = ParseSimple (reader.ReadToEnd ());
				}
				headers = response.Headers;
			}
			if (!contents.ContainsKey (OAuthTokenKey) || !contents.ContainsKey (OAuthTokenSecretKey))
				throw new Exception ();
			_accessToken = contents[OAuthTokenKey];
			_accessTokenSecret = contents[OAuthTokenSecretKey];
			_credentials = new NetworkCredential (username, password);
		}

		public string DownloadString (Uri uri, string method, byte[] postBody)
		{
			WebHeaderCollection headers;
			return DownloadString (uri, method, postBody, out headers);
		}

		public string DownloadString (Uri uri, string method, byte[] postBody, out WebHeaderCollection headers)
		{
			using (HttpWebResponse response = GetResponse (uri, method, _accessToken, _accessTokenSecret, null, null, postBody)) {
				headers = response.Headers;
				using (StreamReader reader = new StreamReader (response.GetResponseStream (), Encoding.ASCII)) {
					return reader.ReadToEnd ();
				}
			}
		}

		public ICredentials Credentials {
			get { return _credentials; }
		}

		private HttpWebResponse GetResponse (Uri uri, string method, string token, string tokenSecret, string pin, string queryString, byte[] postBody)
		{
			string url, reqParam;
			if (queryString != null && queryString.Length > 0) {
				UriBuilder builder = new UriBuilder (uri);
				builder.Query = queryString;
				uri = builder.Uri;
			}
			string sig = GenerateSignature (uri, _consumerKey, _consumerSecret, token, tokenSecret, method.ToString (), GenerateTimeStamp (), GenerateNonce (), out url, out reqParam);

			reqParam += "&" + OAuthSignatureKey + "=" + UrlEncode (sig);
			if (pin != null && pin.Length > 0)
				reqParam += "&oauth_verifier=" + pin;

			HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create (url + "?" + reqParam);
			req.AllowAutoRedirect = false;
			if (method == "POST") {
				req.Method = method.ToString ();
				if (postBody != null && postBody.Length > 0) {
					req.ContentType = UrlEncodedMime;
					req.ContentLength = postBody.Length;
					using (Stream strm = req.GetRequestStream ()) {
						strm.Write (postBody, 0, postBody.Length);
					}
				}
			}

			return (HttpWebResponse)req.GetResponse ();
		}

		protected Dictionary<string, string> ParseSimple (string queryString)
		{
			string[] items = queryString.Split ('&');
			Dictionary<string, string> dic = new Dictionary<string, string> (items.Length);
			for (int i = 0; i < items.Length; i++) {
				string[] values = items[i].Split ('=');
				dic.Add (values[0], values[1]);
			}
			return dic;
		}
	}
}
