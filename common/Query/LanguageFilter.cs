using System;
using System.IO;
using System.Linq;
using Fetcho.Common.Entities;
using LanguageDetection;

namespace Fetcho.Common
{
    [Filter("lang:", "lang:[lang|*][:lang|*]")]
    public class LanguageFilter : Filter
    {
        private LanguageDetector detector;

        public string Language { get; set; }

        public override string Name => "LanguageFilter";

        public override decimal Cost => 50m;

        public override bool RequiresTextInput { get => true; }

        public override bool IsReducingFilter => !String.IsNullOrWhiteSpace(Language);

        public LanguageFilter(string language) : this()
            => Language = language;

        private LanguageFilter()
        {
            detector = new LanguageDetector();
            detector.AddAllLanguages();
        }

        public override string GetQueryText() => string.Format("lang:{0}", Language);

        public override string[] IsMatch(WorkspaceResult result, string fragment, Stream stream)
        {
            var l = detector.DetectAll(fragment).OrderByDescending(x => x.Probability).FirstOrDefault();

            if (l == null) return EmptySet;
            if (String.IsNullOrWhiteSpace(Language)) return new string[] { l.Language };
            if (l.Language == Language) return new string[] { l.Language };
            return EmptySet; 
        }

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
    }

}
