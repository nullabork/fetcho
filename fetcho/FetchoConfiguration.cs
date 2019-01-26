namespace Fetcho
{
    public class FetchoConfiguration
	{
		public int MaxActiveFetches {
			get;
			set;
		}

		public string UriSourceFilePath {
			get;
			set;
		}
	  
	  /// <summary>
	  /// If we're only getting raw URLs
	  /// </summary>
	  public bool InputRawUrls {
	    get; set;
	  }

		public FetchoConfiguration(string[] args)
		{
			parse(args);
		}

		private void parse(string[] args)
		{
			if (args.Length > 0)
				UriSourceFilePath = args[0];
			
      InputRawUrls |= args.Length > 0 && args[0] == "--raw";
      InputRawUrls |= args.Length > 1 && args[1] == "--raw";
		}
	}
}

