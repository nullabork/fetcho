using Microsoft.ML.Data;

namespace learno
{
    public class TextDataLabelPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; }

        public float[] Score;
    }
}
