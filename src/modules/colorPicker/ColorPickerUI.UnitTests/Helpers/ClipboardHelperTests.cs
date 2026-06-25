// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class ClipboardHelperTests
    {
        [TestMethod]
        public void CopyToClipboard_null_is_a_no_op()
        {
            ClipboardHelper.CopyToClipboard(null); // must not throw, must not touch the clipboard
        }

        [TestMethod]
        public void CopyToClipboard_empty_is_a_no_op()
        {
            ClipboardHelper.CopyToClipboard(string.Empty); // must not throw
        }
    }
}
