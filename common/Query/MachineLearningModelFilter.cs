using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fetcho.Common.Entities;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fetcho.Common
{
    [Filter(
        "ml-model(", 
        "ml-model(model_name[, confidence]):[classification|*][:classifcation|*]",
        Description = "Filter or tag by a pre-trained machine learning model"
        )]
    public class MachineLearningModelFilter : Filter
    {
        const float DefaultConfidenceThreshold = 0.7f;
        const string MachineLearningModelFilterKey = "ml-model(";
        private PredictionEngine<PageData, PageClassPrediction> PredictionEngine { get; set; }

        public string ModelName { get; set; }
        public string SearchText { get; set; }
        public float ConfidenceThreshold { get; set; }

        public override string Name => "Machine Learning Model Filter";

        public MachineLearningModelFilter(string modelName, string filterTags, float confidenceThreshold = DefaultConfidenceThreshold)
        {
            ModelName = modelName.CleanInput();
            SearchText = filterTags.CleanInput();
            ConfidenceThreshold = confidenceThreshold.ConstrainRange(0,1);
            PredictionEngine = LoadPredictionEngineFromCache();
        }

        public override string GetQueryText()
            => string.Format("{0}{1}):{2}", MachineLearningModelFilterKey, ModelName, SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            PageClassPrediction prediction = null;
            prediction = PredictionEngine.Predict(new PageData { TextData = fragment });

            if (!String.IsNullOrWhiteSpace(prediction.PredictedLabels) && 
                (String.IsNullOrWhiteSpace(SearchText) || prediction.PredictedLabels.Contains(SearchText)) &&
                prediction.Score.Max() > ConfidenceThreshold)
                return new string[1] { Utility.MakeTag(prediction.PredictedLabels) };
            return EmptySet;
        }

        private PredictionEngine<PageData, PageClassPrediction> LoadPredictionEngineFromCache()
        {
            if (!PredictionEngineCache.ContainsKey(ModelName))
                LoadPredictionEngineFromDiskIntoCache(ModelName);

            return PredictionEngineCache[ModelName];
        }

        private void LoadPredictionEngineFromDiskIntoCache(string modelName)
        {
            string path = Path.Combine(FetchoConfiguration.Current.DataSourcePath, ModelName + ".mlmodel");

            using (var stream = File.OpenRead(path))
                PredictionEngineCache.Add(modelName, MLContext.Model.CreatePredictionEngine<PageData, PageClassPrediction>(MLContext.Model.Load(stream)));
        }

        private static Dictionary<string, PredictionEngine<PageData, PageClassPrediction>> PredictionEngineCache
            = new Dictionary<string, PredictionEngine<PageData, PageClassPrediction>>();

        private static MLContext MLContext = new MLContext();

        /// <summary>
        /// Parse some text to create this object
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string searchText = String.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            var argsString = tokens[0].Substring(MachineLearningModelFilterKey.Length, tokens[0].Length - MachineLearningModelFilterKey.Length - 1);
            var argsTokens = argsString.Split(',');
            var modelName = argsTokens[0];
            float confidence = DefaultConfidenceThreshold;
            if (argsTokens.Length < 2 || !float.TryParse(argsTokens[1], out confidence))
                confidence = DefaultConfidenceThreshold;
            searchText = tokens[1].Trim();

            return new MachineLearningModelFilter(modelName, searchText, confidence);
        }

        private class PageData
        {
            public string TextData;

            public string Label;
        }

        private class PageClassPrediction
        {
            [ColumnName("PredictedLabel")]
            public string PredictedLabels;

            public float[] Score;
        }

    }
}
