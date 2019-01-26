
using System;

[assembly: CLSCompliant(true)]
namespace Fetcho.NextLinks
{
    public class NextLinksConfiguration
	{
		public string SourceLinkFilePath {
			get;
			set;
		}

		public string RejectedLinkFilePath {
			get;
			set;
		}

		public NextLinksConfiguration(string[] args)
		{
            if (args == null) throw new ArgumentNullException("args");

			if (args.Length == 2) {
				SourceLinkFilePath = args[0];
				RejectedLinkFilePath = args[1];
			}
			else if (args.Length == 1) {
				SourceLinkFilePath = args[0];
			}
		}
	}
}

