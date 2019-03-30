using System;

namespace learno
{
    class Program
    {
        static void Main(string[] args)
        {
            //var burgersWorkspace = new Guid("8fed1cb3-7333-4a5f-a000-d38788de2d49");
            var randomWorkspace = new Guid("24f51e4e-951e-453b-957c-1a1dfe475672");

            var schema = new MLModelSchema("science.mlmodel");
            schema.AddDataSource(new RedditMLModelSchemaCategoryDataSource("science", "science"));
            schema.AddDataSource(new FetchoWorkspaceMLModelSchemaCategoryDataSource("random", randomWorkspace));

            var trainer = new MultiClassifierModelTrainer();
            trainer.TrainAndSave(schema);

//            var r = new RedditMLModelSchemaCategoryDataSource("science", "science");
//            var a = r.GetData(5000);
        }
    }
}
