// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Wox.Plugin.Logger;

namespace PowerLauncher.Helper
{
    public static class FontHelper
    {
        private static readonly FontWeightConverter _fontWeightConverter = new FontWeightConverter();

        public static FontWeight GetFontWeightFromInvariantStringOrNormal(string value)
        {
            if (value == null)
            {
                return FontWeights.Normal;
            }

            try
            {
                return (FontWeight)_fontWeightConverter.ConvertFromInvariantString(value);
            }
            catch (NotSupportedException e)
            {
                Log.Exception($"Can't convert {value} to FontWeight", e, MethodBase.GetCurrentMethod().DeclaringType);
                return FontWeights.Normal;
            }
        }

        private static readonly FontStyleConverter _fontStyleConverter = new FontStyleConverter();

        public static FontStyle GetFontStyleFromInvariantStringOrNormal(string value)
        {
            if (value == null)
            {
                return FontStyles.Normal;
            }

            try
            {
                return (FontStyle)_fontStyleConverter.ConvertFromInvariantString(value);
            }
            catch (NotSupportedException e)
            {
                Log.Exception($"Can't convert {value} to FontStyle", e, MethodBase.GetCurrentMethod().DeclaringType);
                return FontStyles.Normal;
            }
        }

        private static readonly FontStretchConverter _fontStretchConverter = new FontStretchConverter();

        public static FontStretch GetFontStretchFromInvariantStringOrNormal(string value)
        {
            if (value == null)
            {
                return FontStretches.Normal;
            }

            try
            {
                return (FontStretch)_fontStretchConverter.ConvertFromInvariantString(value);
            }
            catch (NotSupportedException e)
            {
                Log.Exception($"Can't convert {value} to FontStretch", e, MethodBase.GetCurrentMethod().DeclaringType);
                return FontStretches.Normal;
            }
        }

        public static FamilyTypeface ChooseRegularFamilyTypeface(this FontFamily family)
        {
            ArgumentNullException.ThrowIfNull(family);

            return family.FamilyTypefaces.OrderBy(o =>
            {
                return (Math.Abs(o.Stretch.ToOpenTypeStretch() - FontStretches.Normal.ToOpenTypeStretch()) * 100) +
                    Math.Abs(o.Weight.ToOpenTypeWeight() - FontWeights.Normal.ToOpenTypeWeight()) +
                    ((o.Style == FontStyles.Normal ? 0 : o.Style == FontStyles.Oblique ? 1 : 2) * 1000);
            }).FirstOrDefault() ?? family.FamilyTypefaces.FirstOrDefault();
        }

        public static FamilyTypeface ConvertFromInvariantStringsOrNormal(this FontFamily family, string style, string weight, string stretch)
        {
            ArgumentNullException.ThrowIfNull(family);

            var styleObj = GetFontStyleFromInvariantStringOrNormal(style);
            var weightObj = GetFontWeightFromInvariantStringOrNormal(weight);
            var stretchObj = GetFontStretchFromInvariantStringOrNormal(stretch);
            return family.FamilyTypefaces.FirstOrDefault(o => o.Style == styleObj && o.Weight == weightObj && o.Stretch == stretchObj)
                ?? family.ChooseRegularFamilyTypeface();
        }
    }
}
