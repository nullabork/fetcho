using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        const string DefaultMLModelFileNameExtension = ".mlmodel";
        const float DefaultConfidenceThreshold = 0.98f;
        const string MachineLearningModelFilterKey = "ml-model(";

        private PredictionEngine<PageData, PageClassPrediction> PredictionEngine { get; set; }
        private string lastDataHash = String.Empty;
        private PageClassPrediction lastPrediction = null;

        public string ModelName { get; set; }
        public string SearchText { get; set; }
        public float ConfidenceThreshold { get; set; }

        public override string Name => "Machine Learning Model Filter";

        public override decimal Cost => 500m;

        public override bool RequiresTextInput { get => true; }

        public override bool IsReducingFilter => ConfidenceThreshold > 0f;

        public MachineLearningModelFilter(string modelName, string filterTags, float confidenceThreshold = DefaultConfidenceThreshold)
        {
            ModelName = modelName.CleanInput();
            SearchText = filterTags.CleanInput();
            ConfidenceThreshold = confidenceThreshold.ConstrainRange(0, 1);
            PredictionEngine = LoadPredictionEngineFromCache();
        }

        public override string GetQueryText()
            => string.Format(
                "{0}{1},{2}):{3}",
                MachineLearningModelFilterKey,
                ModelName,
                ConfidenceThreshold,
                String.IsNullOrWhiteSpace(SearchText) ? WildcardChar : SearchText
                );

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {

            if (result.DataHash != lastDataHash)
            {
                lastPrediction = PredictionEngine.Predict(new PageData { TextData = fragment });
                lastDataHash = result.DataHash;
            }

            System.Diagnostics.Debug.WriteLine("Label '{0}', {1}, max(Score) = {2}", lastPrediction.PredictedLabels, ModelName, lastPrediction.Score.Max());

            if (lastPrediction.HasPrediction &&
                lastPrediction.IsMatchingPrediction(SearchText) &&
                lastPrediction.MaxScore > ConfidenceThreshold)
            {
                return new string[1] { Utility.MakeTag(lastPrediction.PredictedLabels) };
            }
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
            string path = Path.Combine(FetchoConfiguration.Current.DataSourcePath, ModelName + DefaultMLModelFileNameExtension);

            if (!File.Exists(path))
                throw new FetchoException("{0} model doesn't exist", ModelName);

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
            //try
            //{
            string searchText = String.Empty;

            var tokens = queryText.Split(':');
            if (tokens.Length != 2) return null;

            var argsString = tokens[0].Substring(
                MachineLearningModelFilterKey.Length,
                tokens[0].Length - MachineLearningModelFilterKey.Length - 1);
            var argsTokens = argsString.Split(',');
            var modelName = argsTokens[0];
            float confidence = DefaultConfidenceThreshold;
            if (argsTokens.Length < 2 || !float.TryParse(argsTokens[1], out confidence))
                confidence = DefaultConfidenceThreshold;
            searchText = tokens[1].Trim();

            return new MachineLearningModelFilter(modelName, searchText, confidence);
            //}
            //catch (Exception ex)
            //{
            //    Utility.LogException(ex);
            //    return null;
            //}
        }

        public static string GetLongHelp()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Available Models:\n");

            foreach (string modelFile in Directory.GetFiles(FetchoConfiguration.Current.DataSourcePath, "*" + DefaultMLModelFileNameExtension))
            {
                sb.AppendFormat("\t{0}\n", Path.GetFileNameWithoutExtension(modelFile));
            }

            return sb.ToString();
        }

        private class PageData
        {
            public string TextData;

            [ColumnName("Label")]
            public string Label;

            [ColumnName("Category")]
            public string Category;
        }

        private class PageClassPrediction
        {
            [ColumnName("PredictedLabel")]
            public string PredictedLabels;

            public float[] Score;

            public bool HasPrediction { get => !String.IsNullOrWhiteSpace(PredictedLabels); }

            public float MaxScore { get => Score.Max(); }

            public bool IsMatchingPrediction(string searchText)
                => String.IsNullOrWhiteSpace(searchText) || PredictedLabels.Contains(searchText);
        }

    }
}
