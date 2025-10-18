// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;

using ImageResizer.Properties;

namespace ImageResizer.Helpers
{
    internal static class AiSuperResolutionFormatter
    {
        private static readonly Lock FormatLock = new Lock();
        private static CultureInfo _cachedCulture;
        private static CompositeFormat _scaleFormat;

        public static string FormatScaleName(int scale)
        {
            return string.Format(CultureInfo.CurrentCulture, GetScaleFormat(), scale);
        }

        private static CompositeFormat GetScaleFormat()
        {
            var culture = Resources.Culture ?? CultureInfo.CurrentUICulture;
            if (_scaleFormat != null && Equals(_cachedCulture, culture))
            {
                return _scaleFormat;
            }

            FormatLock.Enter();
            try
            {
                if (_scaleFormat != null && Equals(_cachedCulture, culture))
                {
                    return _scaleFormat;
                }

                var previousCulture = Resources.Culture;
                try
                {
                    Resources.Culture = culture;
                    _scaleFormat = CompositeFormat.Parse(Resources.Input_AiScaleFormat);
                    _cachedCulture = culture;
                }
                finally
                {
                    Resources.Culture = previousCulture;
                }
            }
            finally
            {
                FormatLock.Exit();
            }

            return _scaleFormat;
        }
    }
}
