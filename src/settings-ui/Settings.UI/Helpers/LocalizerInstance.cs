// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using WinUI3Localizer;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal static class LocalizerInstance
    {
        internal static ILocalizer Instance { get; private set; }

        static LocalizerInstance()
        {
            InitializeLocalizer().Wait();
        }

        private static async Task InitializeLocalizer()
        {
            // Initialize a "Strings" folder in the executables folder.
            string stringsPath = Path.Combine(AppContext.BaseDirectory, "Strings", "Settings");

            Instance = await new LocalizerBuilder()
                .AddStringResourcesFolderForLanguageDictionaries(stringsPath)
                .SetOptions(options =>
                {
                    options.DefaultLanguage = "de-DE";
                    options.UseUidWhenLocalizedStringNotFound = true;
                })
                .Build();
        }
    }
}
