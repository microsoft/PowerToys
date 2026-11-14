// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ImageResizer.Helpers;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Tests.Properties
{
    [TestClass]
    [DoNotParallelize]
    public sealed class LocalizedErrorFallbackTests
    {
        private Func<string, string> _originalGetString;

        [TestInitialize]
        public void Initialize()
        {
            _originalGetString = ResourceLoaderInstance.GetString;
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResourceLoaderInstance.GetString = _originalGetString;
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" \t")]
        [DataRow("CLI_ErrorInvalidInputPath")]
        public void InvalidInputPath_WhenLookupReturnsMissingValueOrExactKey_ReturnsFallback(string lookupValue)
        {
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return lookupValue;
            };

            var result = Resources.CLI_ErrorInvalidInputPath;

            Assert.AreEqual("CLI_ErrorInvalidInputPath", requestedKey);
            Assert.AreEqual("Invalid input path: {0}", result);
        }

        [TestMethod]
        [DataRow("MiXeD translated path: {0}")]
        [DataRow("cli_errorinvalidinputpath")]
        public void InvalidInputPath_WhenLookupReturnsDistinctValue_PreservesValue(string lookupValue)
        {
            string requestedKey = null;
            ResourceLoaderInstance.GetString = key =>
            {
                requestedKey = key;
                return lookupValue;
            };

            var result = Resources.CLI_ErrorInvalidInputPath;

            Assert.AreEqual("CLI_ErrorInvalidInputPath", requestedKey);
            Assert.AreEqual(lookupValue, result);
        }
    }
}
