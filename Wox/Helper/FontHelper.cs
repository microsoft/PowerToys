using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using Wox.Helper;

namespace Wox.Helper
{
    public static class FontHelper
    {
        static FontWeightConverter fontWeightConverter = new FontWeightConverter();
        public static FontWeight GetFontWeightFromInvariantStringOrNormal(string value)
        {
            try
            {
                return (FontWeight) fontWeightConverter.ConvertFromInvariantString(value);
            }
            catch {
                return FontWeights.Normal;
            }
        }

        static FontStyleConverter fontStyleConverter = new FontStyleConverter();
        public static FontStyle GetFontStyleFromInvariantStringOrNormal(string value)
        {
            try
            {
                return (FontStyle)fontStyleConverter.ConvertFromInvariantString(value);
            }
            catch
            {
                return FontStyles.Normal;
            }
        }

        static FontStretchConverter fontStretchConverter = new FontStretchConverter();
        public static FontStretch GetFontStretchFromInvariantStringOrNormal(string value)
        {
            try
            {
                return (FontStretch)fontStretchConverter.ConvertFromInvariantString(value);
            }
            catch
            {
                return FontStretches.Normal;
            }
        }

        public static FamilyTypeface ChooseRegularFamilyTypeface(this FontFamily family)
        {
            return family.FamilyTypefaces.OrderBy(o =>
            {
                return Math.Abs(o.Stretch.ToOpenTypeStretch() - FontStretches.Normal.ToOpenTypeStretch()) * 100 +
                    Math.Abs(o.Weight.ToOpenTypeWeight() - FontWeights.Normal.ToOpenTypeWeight()) +
                    (o.Style == FontStyles.Normal ? 0 : o.Style == FontStyles.Oblique ? 1 : 2) * 1000;
            }).FirstOrDefault() ?? family.FamilyTypefaces.FirstOrDefault();
        }

        public static FamilyTypeface ConvertFromInvariantStringsOrNormal(this FontFamily family, string style, string weight, string stretch)
        {
            var styleObj = GetFontStyleFromInvariantStringOrNormal(style);
            var weightObj = GetFontWeightFromInvariantStringOrNormal(weight);
            var stretchObj = GetFontStretchFromInvariantStringOrNormal(stretch);
            return family.FamilyTypefaces.FirstOrDefault(o => o.Style == styleObj && o.Weight == weightObj && o.Stretch == stretchObj)
                ?? family.ChooseRegularFamilyTypeface();
        }

    }
}
