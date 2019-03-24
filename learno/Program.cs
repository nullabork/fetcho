using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fetcho.Common;
using Fetcho.Common.Entities;
using Fetcho.ContentReaders;
using Microsoft.Data.DataView;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace learno
{
    class Program
    {
        static void Main(string[] args)
        {
            var burgersWorkspace = new Guid("8fed1cb3-7333-4a5f-a000-d38788de2d49");
            var randomWorkspace = new Guid("24f51e4e-951e-453b-957c-1a1dfe475672");

            var schema = new MLModelSchema("burgers.mlmodel");
            schema.Workspaces.Add(burgersWorkspace, "burgers");
            schema.Workspaces.Add(randomWorkspace, "notburgers");

            var trainer = new MultiClassifierModelTrainer();
            trainer.TrainAndSave(schema);

            Console.WriteLine("Press any key to exit....");
            Console.ReadLine();
        }
    }

    public class TextPageData
    {
        public string TextData { get; set; }

        public string Label { get; set; }
    }

    public class TextDataClassPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; }
    }

    public class MLModelSchema
    {
        public const int DefaultMaxResultsPerWorkspace = 500;

        public Dictionary<Guid, string> Workspaces { get; }

        public string FilePath { get; set; }

        public bool OverwriteExistingModelFile { get; set; }

        public int MaxResultsPerWorkspace { get; set; }

        public MLModelSchema(string prototypeFileName)
        {
            Workspaces = new Dictionary<Guid, string>();
            OverwriteExistingModelFile = false;
            FilePath = prototypeFileName;
            MaxResultsPerWorkspace = DefaultMaxResultsPerWorkspace;
        }
    }

    public class MultiClassifierModelTrainer
    {
        public const string DefaultMLModelFileNameExtension = ".mlmodel";

        public MultiClassifierModelTrainer()
        {
        }

        public void TrainAndSave(MLModelSchema schema)
        {
            var data = GetAllData(schema);

            MLContext mlContext = new MLContext(null, 1); // 1 concurrency, leave me some CPUs plz

            IDataView trainingDataView = mlContext.Data.LoadFromEnumerable(data);

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey(inputColumnName: "TextData", outputColumnName: "Label")
                .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName: "TextData", outputColumnName: "TextDataFeaturized"))
                .Append(mlContext.Transforms.Concatenate("Features", "TextDataFeaturized"))
                .AppendCacheCheckpoint(mlContext)
                .Append(mlContext.MulticlassClassification.Trainers.StochasticDualCoordinateAscent(DefaultColumnNames.Label, DefaultColumnNames.Features))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // STEP 4: Train your model based on the data set  
            var model = pipeline.Fit(trainingDataView);

            using (var fs = File.OpenWrite(schema.FilePath))
                mlContext.Model.Save(model, fs);

        }

        private IEnumerable<TextPageData> GetAllData(MLModelSchema schema)
        {
            var l = new List<TextPageData>();
            using (var db = new Database())
            {
                foreach (var kvp in schema.Workspaces)
                {
                    var results = db.GetWorkspaceResultsByRandom(kvp.Key, schema.MaxResultsPerWorkspace).GetAwaiter().GetResult();
                    l.AddRange(GetPages(db, kvp.Value, results));
                }
            }
            return l;
        }

        private IEnumerable<TextPageData> GetPages(Database db, string label, IEnumerable<WorkspaceResult> results)
        {
            var parser = new BracketPipeTextExtractor();

            var l = new List<TextPageData>();

            foreach (var result in results)
            {
                if (string.IsNullOrWhiteSpace(result.DataHash)) continue;
                var data = db.GetWebResourceCacheData(new MD5Hash(result.DataHash)).GetAwaiter().GetResult();

                if (data == null) continue;

                var t = new List<string>();
                using (var ms = new MemoryStream(data))
                    parser.Parse(ms, t.Add);

                l.Add(new TextPageData
                {
                    TextData = t.Aggregate("", (x, y) => x + " " + y),
                    Label = label
                });
            }

            return l;
        }

    }
}
