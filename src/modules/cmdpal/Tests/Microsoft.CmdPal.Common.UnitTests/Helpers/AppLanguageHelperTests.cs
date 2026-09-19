// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class AppLanguageHelperTests
{
    [TestMethod]
    public void Apply_SetsDotNetUiCultureToRequestedLanguage()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            AppLanguageHelper.Apply("ja-JP");

            Assert.IsTrue(string.Equals("ja-JP", AppLanguageHelper.LanguageOverride, StringComparison.Ordinal));
            Assert.IsTrue(string.Equals("ja-JP", CultureInfo.CurrentUICulture.Name, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals("ja-JP", CultureInfo.DefaultThreadCurrentUICulture?.Name, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
            AppLanguageHelper.Apply(string.Empty);
        }
    }

    [TestMethod]
    public void Apply_EmptyTag_ClearsLanguageOverrideWithoutThrowing()
    {
        var originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            AppLanguageHelper.Apply("de-DE");
            AppLanguageHelper.Apply(string.Empty);

            Assert.IsTrue(string.Equals(string.Empty, AppLanguageHelper.LanguageOverride, StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalUiCulture;
            AppLanguageHelper.Apply(string.Empty);
        }
    }

    [TestMethod]
    public void Apply_UnknownTag_DoesNotThrow()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        var originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;

        try
        {
            AppLanguageHelper.Apply("not-a-valid-bcp47-tag");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalDefaultCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalDefaultUiCulture;
            AppLanguageHelper.Apply(string.Empty);
        }
    }
}
