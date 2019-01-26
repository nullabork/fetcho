namespace Fetcho
{
    class ExtractoConfiguration
	{
		public string DataSourceFilePath {
			get;
			set;
		}

		public ExtractoConfiguration(string[] args)
		{
			if (args.Length > 0)
				DataSourceFilePath = args[0];
		}
	}
}

