// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using WorkspacesLauncherUI.Properties;

namespace WorkspacesLauncherUI.UnitTests
{
    internal sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _resourceCulture = Resources.Culture;

        public CultureScope(string cultureName)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            Resources.Culture = null;
        }

        public void Dispose()
        {
            Resources.Culture = _resourceCulture;
            CultureInfo.CurrentUICulture = _uiCulture;
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
