// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using WorkspacesLauncherUI.Properties;

namespace WorkspacesLauncherUI.UnitTests
{
    internal sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
        private readonly CultureInfo _resourceCulture = Resources.Culture;
        private readonly FieldInfo _managerField = typeof(Resources).GetField("resourceMan", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly ResourceManager _manager = Resources.ResourceManager;

        public CultureScope(string cultureName)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            Resources.Culture = null;
            _managerField.SetValue(null, new ResourceManager("WorkspacesLauncherUI.Properties.Resources", typeof(CultureScope).Assembly));
        }

        public void Dispose()
        {
            Resources.Culture = _resourceCulture;
            _managerField.SetValue(null, _manager);
            CultureInfo.CurrentUICulture = _uiCulture;
            CultureInfo.CurrentCulture = _culture;
        }
    }
}
