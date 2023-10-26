// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZonesEditor
{
    [TestClass]
    public class RunFancyZonesEditorTest
    {
        private static FancyZonesEditorSession? _session;
        private static TestContext? _context;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _context = testContext;
            _session = new FancyZonesEditorSession(testContext);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _session?.Close(_context!);
        }

        [TestMethod]
        public void RunFancyZonesEditor()
        {
            Assert.IsNotNull(_session?.Session);
        }
    }
}
