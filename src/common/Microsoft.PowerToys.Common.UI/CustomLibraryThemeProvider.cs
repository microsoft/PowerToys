// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using ControlzEx.Theming;

namespace Microsoft.PowerToys.Common.UI
{
    public class CustomLibraryThemeProvider : LibraryThemeProvider
    {
        public static readonly CustomLibraryThemeProvider DefaultInstance = new CustomLibraryThemeProvider();

        public CustomLibraryThemeProvider()
            : base(true)
        {
        }

        /// <inheritdoc />
        public override void FillColorSchemeValues(Dictionary<string, string> values, RuntimeThemeColorValues colorValues)
        {
        }
    }
}
