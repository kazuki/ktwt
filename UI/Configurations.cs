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
using System.Linq;
using System.Text;
using System.IO;
using ktwt.Json;
using ktwt.Twitter;

namespace ktwt.ui
{
	public class Configurations
	{
		public static Configurations Load (string path)
		{
			try {
				if (File.Exists (path)) {
					using (StreamReader reader = new StreamReader (path)) {
						return JsonDeserializer.Deserialize<Configurations> (reader.ReadToEnd ());
					}
				}
			} catch {}
			return new Configurations { ConfigurationFilePath = path };
		}

		Configurations () {}

		public void Save ()
		{
			using (StreamWriter writer = new StreamWriter (ConfigurationFilePath)) {
				JsonSerializer.Serialize (writer, this);
			}
		}

		public string ConfigurationFilePath { get; set; }

		[JsonObjectMapping ("twitter_accounts", JsonValueType.Array)]
		public TwitterOAuthCredentialCache[] TwitterAccounts { get; set; }
	}
}
