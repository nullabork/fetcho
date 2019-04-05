using System;

namespace learno
{
    class Program
    {
        static void Main(string[] args)
        {
            var scienceWorkspace = new Guid("698ff939-3eb4-4e65-b93b-ccde24c2838e");
            var randomWorkspace = new Guid("24f51e4e-951e-453b-957c-1a1dfe475672");

            var schema = new MLModelSchema("science2.mlmodel");
            schema.AddDataSource(new FetchoWorkspaceMLModelSchemaCategoryDataSource("science", scienceWorkspace));
            schema.AddDataSource(new FetchoWorkspaceMLModelSchemaCategoryDataSource("random", randomWorkspace) {
                UseNameForCategory = true
            });

            var trainer = new MultiClassifierModelTrainer();
            trainer.TrainAndSave(schema);

//            var r = new RedditMLModelSchemaCategoryDataSource("science", "science");
//            var a = r.GetData(5000);
        }
    }
}
