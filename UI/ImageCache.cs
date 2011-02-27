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
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ktwt.ui
{
	public class ImageCache
	{
		string _dir;
		Stack<string> _stack = new Stack<string> ();
		HashSet<string> _stackUrls = new HashSet<string> ();
		int _downloading = 0;
		int _maxDownloading = 8;
		LRU<string, ImageSource> _memCache;
		System.Windows.Size _size;

		public event EventHandler DownloadCompleted;

		public ImageCache (string cache_dir, System.Windows.Size size)
		{
			try {
				if (!Directory.Exists (cache_dir))
					Directory.CreateDirectory (cache_dir);
			} catch {}
			_dir = cache_dir;
			_size = size;

			LRU<string, ImageSource>.CreateDelegate create = delegate (string key) {
				try {
					Uri uri = new Uri (key);
					BitmapSource bi = null;
					try {
						bi = new BitmapImage (uri);
					} catch {
						using (Bitmap bmp = new Bitmap (uri.LocalPath)) {
							bi = Imaging.CreateBitmapSourceFromHBitmap (bmp.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight (bmp.Width, bmp.Height));
						}
					}
					if (bi == null)
						return null;
					return new CachedBitmap (new TransformedBitmap (bi, new ScaleTransform (_size.Width / bi.Width, _size.Height / bi.Height)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
				} catch {
					return null;
				}
			};
			_memCache = new LRU<string, ImageSource> (create, 2048);
		}

		public ImageSource LoadCache (string url)
		{
			string filePath = UrlToCachePath (url);
			if (File.Exists (filePath))
				return _memCache.Get ("file://" + Path.GetFullPath (filePath));
			AddDownloadQueue (url);
			return null;
		}

		public System.Windows.Size ImageSize {
			get { return _size; }
			set {
				_size = value;
				_memCache.Clear ();
			}
		}

		string UrlToKey (string url)
		{
			string ext = Path.GetExtension (url);
			url = url.Replace ("http://", "").Replace (".twimg.com/profile_images", "");
			url = url.Substring (0, url.Length - ext.Length);
			byte[] raw = Encoding.ASCII.GetBytes (url);
			string key = Convert.ToBase64String (raw);
			return key.Replace ('+', '-').Replace ('/', '#') + ext;
		}

		string KeyToCachePath (string key)
		{
			return Path.Combine (_dir, key);
		}

		string UrlToCachePath (string url)
		{
			return KeyToCachePath (UrlToKey (url));
		}

		void AddDownloadQueue (string url)
		{
			lock (_stack) {
				if (!_stackUrls.Add (url))
					return;
				if (_downloading >= _maxDownloading) {
					_stack.Push (url);
				} else {
					_downloading ++;
					ThreadPool.QueueUserWorkItem (DownloadThread, url);
				}
			}
		}

		void DownloadThread (object o)
		{
			string url = (string)o;
			byte[] raw = null;
			bool completed = false;
			try {
				using (WebClient client = new WebClient ()) {
					raw = client.DownloadData (url);
				}
				if (raw.Length > 0) {
					File.WriteAllBytes (UrlToCachePath (url), raw);
					completed = true;
				}
			} catch {}

			lock (_stack) {
				_stackUrls.Remove (url);
				_downloading --;

				if (_stack.Count > 0) {
					_downloading ++;
					ThreadPool.QueueUserWorkItem (DownloadThread, _stack.Pop ());
				}
			}

			if (completed && DownloadCompleted != null)
				DownloadCompleted (this, EventArgs.Empty);
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
					if (_cache.Count > _max_entries) {
						_cache.Remove (_lru.First.Value.Key);
						_lru.RemoveFirst ();
					}
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
}
