// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.DevDocs.UnitTest
{
    [TestClass]
    public class MainTests
    {
        [TestMethod]
        public void EmptyQueryReturnsNoResults()
        {
            var main = new Main();

            var results = main.Query(new Query(string.Empty));

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void PluginMetadataIsResolvedFromResources()
        {
            var main = new Main();

            Assert.AreEqual("DevDocs Search", main.Name);
            Assert.AreEqual("Searching in documentation", main.Description);
        }

        [TestMethod]
        public void TranslatedMetadataMatchesPluginMetadata()
        {
            var main = new Main();

            Assert.AreEqual(main.Name, main.GetTranslatedPluginTitle());
            Assert.AreEqual(main.Description, main.GetTranslatedPluginDescription());
        }
    }
}
