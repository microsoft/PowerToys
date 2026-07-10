// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class PowerDisplay
    {
        [TestMethod]
        public void DisposeShouldDisposeProfileOperationsCoordinator()
        {
            var repoRoot = FindRepoRoot();
            var sourcePath = Path.Combine(repoRoot, @"src\settings-ui\Settings.UI\ViewModels\PowerDisplayViewModel.cs");
            var source = File.ReadAllText(sourcePath);

            var disposeMethodIndex = source.IndexOf("public override void Dispose()", StringComparison.Ordinal);
            Assert.IsTrue(disposeMethodIndex >= 0, "PowerDisplayViewModel.Dispose() was not found.");

            var coordinatorDisposeIndex = source.IndexOf("_profileOperations.Dispose();", StringComparison.Ordinal);
            Assert.IsTrue(
                coordinatorDisposeIndex > disposeMethodIndex,
                "PowerDisplayViewModel.Dispose() should call _profileOperations.Dispose().");
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !string.Equals(directory.Name, "pd-profile-id", StringComparison.OrdinalIgnoreCase))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "Could not locate the repository root.");
            return directory.FullName;
        }
    }
}
