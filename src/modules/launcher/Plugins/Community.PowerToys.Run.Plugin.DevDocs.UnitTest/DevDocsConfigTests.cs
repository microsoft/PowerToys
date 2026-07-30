// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Community.PowerToys.Run.Plugin.DevDocs.UnitTest
{
    [TestClass]
    public class DevDocsConfigTests
    {
        [DataTestMethod]
        [DataRow("js", "javascript")]
        [DataRow("py", "python")]
        [DataRow("rb", "ruby")]
        [DataRow("golang", "go")]
        [DataRow("pg", "postgresql")]
        [DataRow("next", "nextjs")]
        [DataRow("rn", "react_native")]
        [DataRow("drf", "django_rest_framework")]
        [DataRow("exp", "express")]
        public void AliasMapResolvesKnownAlias(string alias, string expected)
        {
            Assert.IsTrue(DevDocsConfig.AliasMap.TryGetValue(alias, out var actual));
            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow("ex")]
        [DataRow("ror")]
        [DataRow("k8s")]
        public void AliasMapDoesNotShadowDocumentationProvidedAliases(string alias)
        {
            // DevDocs serves these aliases itself; remapping them would redirect the query elsewhere.
            Assert.IsFalse(DevDocsConfig.AliasMap.ContainsKey(alias));
        }

        [DataTestMethod]
        [DataRow("JS")]
        [DataRow("Py")]
        [DataRow("GoLang")]
        public void AliasMapIsCaseInsensitive(string alias)
        {
            Assert.IsTrue(DevDocsConfig.AliasMap.ContainsKey(alias));
        }

        [TestMethod]
        public void AliasMapDoesNotResolveUnknownAlias()
        {
            Assert.IsFalse(DevDocsConfig.AliasMap.ContainsKey("not-a-real-alias"));
        }

        [TestMethod]
        public void ApiUrlsArePointingAtDevDocs()
        {
            Assert.AreEqual("https://devdocs.io/docs.json", DevDocsConfig.DevDocsApiUrl);
            Assert.AreEqual("https://documents.devdocs.io", DevDocsConfig.DocumentsBaseUrl);
        }
    }
}
