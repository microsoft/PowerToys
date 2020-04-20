using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Wox.Plugin.Caculator
{
    /// <summary>
    /// Tries to convert all numbers in a text from one culture format to another.
    /// </summary>
    public class NumberTranslator
    {
        private readonly CultureInfo sourceCulture;
        private readonly CultureInfo targetCulture;
        private readonly Regex splitRegexForSource;
        private readonly Regex splitRegexForTarget;

        private NumberTranslator(CultureInfo sourceCulture, CultureInfo targetCulture)
        {
            this.sourceCulture = sourceCulture;
            this.targetCulture = targetCulture;

            this.splitRegexForSource = GetSplitRegex(this.sourceCulture);
            this.splitRegexForTarget = GetSplitRegex(this.targetCulture);
        }

        /// <summary>
        /// Create a new <see cref="NumberTranslator"/> - returns null if no number conversion
        /// is required between the cultures.
        /// </summary>
        /// <param name="sourceCulture">source culture</param>
        /// <param name="targetCulture">target culture</param>
        /// <returns></returns>
        public static NumberTranslator Create(CultureInfo sourceCulture, CultureInfo targetCulture)
        {
            bool conversionRequired = sourceCulture.NumberFormat.NumberDecimalSeparator != targetCulture.NumberFormat.NumberDecimalSeparator
                                      || sourceCulture.NumberFormat.PercentGroupSeparator != targetCulture.NumberFormat.PercentGroupSeparator
                                      || sourceCulture.NumberFormat.NumberGroupSizes != targetCulture.NumberFormat.NumberGroupSizes;
            return conversionRequired
                ? new NumberTranslator(sourceCulture, targetCulture)
                : null;
        }

        /// <summary>
        /// Translate from source to target culture.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string Translate(string input)
        {
            return this.Translate(input, this.sourceCulture, this.targetCulture, this.splitRegexForSource);
        }
        
        /// <summary>
        /// Translate from target to source culture.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string TranslateBack(string input)
        {
            return this.Translate(input, this.targetCulture, this.sourceCulture, this.splitRegexForTarget);
        }

        private string Translate(string input, CultureInfo cultureFrom, CultureInfo cultureTo, Regex splitRegex)
        {
            var outputBuilder = new StringBuilder();

            string[] tokens = splitRegex.Split(input);
            foreach (string token in tokens)
            {
                decimal number;
                outputBuilder.Append(
                    decimal.TryParse(token, NumberStyles.Number, cultureFrom, out number)
                    ? number.ToString(cultureTo)
                    : token);
            }

            return outputBuilder.ToString();
        }

        private Regex GetSplitRegex(CultureInfo culture)
        {
            var splitPattern = $"((?:\\d|{Regex.Escape(culture.NumberFormat.NumberDecimalSeparator)}";
            if (!string.IsNullOrEmpty(culture.NumberFormat.NumberGroupSeparator))
            {
                splitPattern += $"|{Regex.Escape(culture.NumberFormat.NumberGroupSeparator)}";
            }
            splitPattern += ")+)";
            return new Regex(splitPattern);
        }
    }
}
