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
		static HashSet<ulong> _friends = new HashSet<ulong> ();
		static LRU<Uri, BitmapImage> _friendsCache;
		static LRU<Uri, BitmapImage> _nonfriendCache;

		public static void Init (TwitterAccountManager mgr)
		{
			LRU<Uri, BitmapImage>.CreateDelegate create = delegate (Uri key) {return new BitmapImage (key);};
			_friendsCache = new LRU<Uri,BitmapImage> (create, 2048);
			_nonfriendCache = new LRU<Uri, BitmapImage> (create, 1024);

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

		public static LRU<Uri,BitmapImage> FriendsCache {
			get { return _friendsCache; }
		}

		public static LRU<Uri,BitmapImage> NonFriendCache {
			get { return _nonfriendCache; }
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
			bool is_friend;
			if (user_id > 0) {
				lock (_friends) {
					is_friend = _friends.Contains (user_id);
				}
			} else {
				is_friend = false;
			}
			return (is_friend ? _friendsCache : _nonfriendCache).Get (uri);
		}

		public sealed class LRU<K,T>
		{
			int _max_entries;
			LinkedList<CacheItem> _lru = new LinkedList<CacheItem> ();
			Dictionary<K, CacheItem> _cache = new Dictionary<K, CacheItem> ();
			CreateDelegate _create;

			public LRU (CreateDelegate create, int maxEntries)
			{
				_create = create;
				_max_entries = maxEntries;
			}

			public T Get (K key)
			{
				lock (_lru) {
					CacheItem item;
					if (_cache.TryGetValue (key, out item)) {
						_lru.Remove (item.Node);
						_lru.AddLast (item.Node);
						return item.Value;
					}

					item = new CacheItem (key, _create (key));
					_lru.AddLast (item.Node);
					_cache.Add (key, item);
					if (_cache.Count > _max_entries)
						_lru.RemoveFirst ();
					return item.Value;
				}
			}

			public void Clear ()
			{
				lock (_lru) {
					_lru.Clear ();
					_cache.Clear ();
				}
			}

			public int Count {
				get { return _cache.Count; }
			}

			sealed class CacheItem
			{
				public CacheItem (K key, T value)
				{
					this.Key = key;
					this.Value = value;
					this.Node = new LinkedListNode<CacheItem> (this);
				}

				public K Key { get; private set; }
				public T Value { get; private set; }
				public LinkedListNode<CacheItem> Node { get; private set; }
			}

			public delegate T CreateDelegate (K key);
		}
	}

	public class ImageCacheConverter : IValueConverter
	{
		public object Convert (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			try {
				if (value is string) {
					string s = (string)value;
					if (s.Length == 0)
						return null;
					return IconCache.GetImage (s);
				}
				if (value is Uri)
					return IconCache.GetImage ((Uri)value);
				if (value is User) {
					User user = (User)value;
					return IconCache.GetImage (user.ID, user.ProfileImageUrl);
				}
			} catch {}

			return null;
		}

		public object ConvertBack (object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotSupportedException ();
		}
	}
}
