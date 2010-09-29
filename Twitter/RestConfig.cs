using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ktwt.Twitter
{
	public static class RestConfig
	{
		public class UserTimeline : IRestUsageConfig
		{
			public ulong? UserId { get; set; }
			public string ScreenName { get; set; }
		}

		public interface IRestUsageConfig {}
	}
}
