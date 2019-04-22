using System;
using System.IO;

namespace learno
{
    class Program
    {
        static void Main(string[] args)
        {
            var hackernewsWorkspace = new Guid("2480be4a-ca1a-4cae-8bb3-654aa0ba8740");
            var scienceWorkspace = new Guid("698ff939-3eb4-4e65-b93b-ccde24c2838e");
            var newsAndGossipWorkspace = new Guid("0e4622a6-ef4b-4308-b18d-9a4a8a71a50c");
            var randomWorkspace = new Guid("24f51e4e-951e-453b-957c-1a1dfe475672");

            var schema = new MLModelSchema("news.mlmodel");
            schema.AddDataSource(new FetchoWorkspaceMLModelSchemaCategoryDataSource("news", newsAndGossipWorkspace)
            {
                UseNameForCategory = true
            });
            schema.AddDataSource(new FetchoWorkspaceMLModelSchemaCategoryDataSource("random", randomWorkspace) {
                UseNameForCategory = true
            });

            schema.BalanceResults = false;

            var trainer = new MultiClassifierModelTrainer();
            trainer.TrainAndSave(schema);

            var fi = new FileInfo(schema.FilePath);
            Console.WriteLine("Size: {0}kb", fi.Length/1024);

//            var r = new RedditMLModelSchemaCategoryDataSource("science", "science");
//            var a = r.GetData(5000);
        }
    }
}
