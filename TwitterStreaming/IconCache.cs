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
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ktwt.Twitter;

namespace TwitterStreaming
{
	public static class IconCache
	{
		public static int MaxCacheEntries = 8192;
		static LinkedList<CacheItem> _lru = new LinkedList<CacheItem> ();
		static Dictionary<Uri, CacheItem> _lru_cache = new Dictionary<Uri, CacheItem> ();
		static HashSet<ulong> _friends = new HashSet<ulong> ();

		public static void Init (TwitterAccountManager mgr)
		{
			mgr.AccountsPropertyChanged += delegate (object sender, EventArgs e) {
				TwitterAccount[] accounts = mgr.Accounts;
				for (int i = 0; i < accounts.Length; i ++) {
					accounts[i].TwitterClient.PropertyChanged -= TwitterClient_PropertyChanged;
					accounts[i].TwitterClient.PropertyChanged += TwitterClient_PropertyChanged;
				}
			};
		}

		static void TwitterClient_PropertyChanged (object sender, PropertyChangedEventArgs e)
		{
			TwitterClient client = (TwitterClient)sender;
			if (!e.PropertyName.Equals ("Friends"))
				return;
			HashSet<ulong> friends = client.FriendSet;
			lock (_friends) {
				_friends.UnionWith (friends);
			}
		}

		public static int GetNumberOfEntries ()
		{
			lock (_lru_cache) {
				return _lru_cache.Count;
			}
		}

		public static BitmapImage GetImage (string uri)
		{
			return GetImage (new Uri (uri));
		}

		public static BitmapImage GetImage (Uri uri)
		{
			return GetImage (0, uri);
		}

		public static BitmapImage GetImage (ulong user_id, string uri)
		{
			return GetImage (user_id, new Uri (uri));
		}

		public static BitmapImage GetImage (ulong user_id, Uri uri)
		{
			if (user_id != 0) {
				bool is_friend;
				lock (_friends) {
					is_friend = _friends.Contains (user_id);
				}
				if (!is_friend)
					return new BitmapImage (uri);
			}

			lock (_lru_cache) {
				CacheItem item;
				if (_lru_cache.TryGetValue (uri, out item)) {
					_lru.Remove (item.Node);
					_lru.AddLast (item.Node);
					return item.Image;
				}

				item = new CacheItem (uri);
				_lru.AddLast (item.Node);
				_lru_cache.Add (uri, item);
				if (_lru_cache.Count > MaxCacheEntries)
					_lru.RemoveFirst ();
				return item.Image;
			}
		}

		sealed class CacheItem
		{
			public CacheItem (Uri uri)
			{
				this.Uri = uri;
				this.Image = new BitmapImage (uri);
				this.Node = new LinkedListNode<CacheItem> (this);
			}

			public Uri Uri { get; private set; }
			public BitmapImage Image { get; private set; }
			public LinkedListNode<CacheItem> Node { get; private set; }
		}
	}

	public class ImageCacheConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return IconCache.GetImage ((string)value);
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException ();
		}
	}
}
