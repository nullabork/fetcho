using System;
using System.Collections.Generic;
using System.IO;
using Fetcho.Common.Entities;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace Fetcho.Common
{
    [Filter(
        "ml-model(", 
        "ml-model(model_name):[search_text|*][:search_text|*]",
        Description = "Filter or tag by a pre-trained machine learning model"
        )]
    public class MachineLearningModelFilter : Filter
    {
        const string MachineLearningModelFilterKey = "ml-model(";
        private PredictionEngine<PageData, PageClassPrediction> PredictionEngine { get; set; }

        public string ModelName { get; set; }
        public string SearchText { get; set; }

        public override string Name => "Machine Learning Model Filter";

        public MachineLearningModelFilter(string modelName, string filterTags)
        {
            ModelName = modelName;
            SearchText = filterTags;
            PredictionEngine = LoadPredictionEngineFromCache();
        }

        public override string GetQueryText()
            => string.Format("{0}{1}):{2}", MachineLearningModelFilterKey, ModelName, SearchText);

        public override string[] IsMatch(IWebResource resource, string fragment, Stream stream)
        {
            PageClassPrediction prediction = null;
            prediction = PredictionEngine.Predict(new PageData { InputData = fragment });

            if (!String.IsNullOrWhiteSpace(prediction.Category))
                return new string[1] { Utility.MakeTag(prediction.Category) };
            return EmptySet;
        }

        private PredictionEngine<PageData, PageClassPrediction> LoadPredictionEngineFromCache()
        {
            if (!PredictionEngineCache.ContainsKey(ModelName))
                LoadPredictionEngineFromDiskIntoCache(ModelName);

            return PredictionEngineCache[ModelName];
        }

        private PredictionEngine<PageData, PageClassPrediction> LoadPredictionEngineFromDiskIntoCache(string modelName)
        {
            string path = Path.Combine(FetchoConfiguration.Current.DataSourcePath, ModelName + ".mlmodel");

            using (var stream = File.OpenRead(path))
            return MLContext.Model.CreatePredictionEngine<PageData, PageClassPrediction>(MLContext.Model.Load(stream));
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

            var key = tokens[0].Substring(MachineLearningModelFilterKey.Length, tokens[0].Length - MachineLearningModelFilterKey.Length - 1);
            searchText = tokens[1].Trim();

            return new MachineLearningModelFilter(key, searchText);
        }

        private class PageData
        {
            [LoadColumn(0)]
            public string InputData;

            [LoadColumn(1)]
            public string Category;
        }

        private class PageClassPrediction
        {
            [ColumnName("PredictedLabel")]
            public string Category;
        }

    }
}
