using System;
using System.IO;
using System.Linq;
using Fetcho.Common;
using Microsoft.Data.DataView;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace learno
{
    public class MultiClassifierModelTrainer
    {
        public const string DefaultMLModelFileNameExtension = ".mlmodel";

        public MultiClassifierModelTrainer()
        {
        }

        public void TrainAndSave(MLModelSchema schema)
        {
            var data = schema.GetAllData();

            foreach (var category in data.Select(x => x.Category).Distinct())
                Console.WriteLine(category);

            MLContext mlContext = new MLContext(null, 1); // 1 concurrency, leave me some CPUs plz

            IDataView trainingDataView = mlContext.Data.LoadFromEnumerable(data);

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Category")
                .Append(mlContext.Transforms.Text.FeaturizeText(inputColumnName: "TextData", outputColumnName: "TextDataFeaturized"))
                .Append(mlContext.Transforms.Concatenate("Features", "TextDataFeaturized"))
                .AppendCacheCheckpoint(mlContext)
                .Append(mlContext.MulticlassClassification.Trainers.StochasticDualCoordinateAscent("Category", DefaultColumnNames.Features))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // STEP 4: Train your model based on the data set  
            var model = pipeline.Fit(trainingDataView);

            using (var fs = File.OpenWrite(schema.FilePath))
                mlContext.Model.Save(model, fs);

        }


    }
}
