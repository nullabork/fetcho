using System;
using System.Linq;
using LanguageDetection;

namespace Fetcho.Common
{
    public class LanguageFilter : Filter
    {
        private LanguageDetector detector;

        public string Language { get; set; }

        public override string Name => "LanguageFilter";

        public LanguageFilter(string language) : this()
            => Language = language;

        private LanguageFilter()
        {
            detector = new LanguageDetector();
            detector.AddAllLanguages();
        }

        public override string GetQueryText() => string.Format("lang:{0}", Language);

        public override string[] IsMatch(Uri uri, string fragment)
            => new string[] { detector.DetectAll(fragment).OrderByDescending(x => x.Probability).FirstOrDefault()?.Language };

        /// <summary>
        /// Parse some text into a TextMatchFilter
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public static Filter Parse(string queryText)
        {
            string language = String.Empty;

            int index = queryText.IndexOf(':');
            if ( index > -1 )
            {
                language = queryText.Substring(index + 1);
                if (language == "*") language = String.Empty;
            }

            return new LanguageFilter(language);
        }

        public static bool TokenIsFilter(string token) => token.StartsWith("lang:");
    }

}
