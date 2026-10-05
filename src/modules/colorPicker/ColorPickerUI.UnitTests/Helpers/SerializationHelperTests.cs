// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class SerializationHelperTests
    {
        private static readonly Dictionary<string, Dictionary<string, string>> ExportedColors = new()
        {
            ["color1"] = new Dictionary<string, string>
            {
                ["HEX"] = "#123456",
            },
        };

        [TestMethod]
        public void Txt_extension_produces_text_content()
        {
            string content = ExportedColors.ToFileContent(".txt");

            StringAssert.Contains(content, "color1;HEX#123456");
        }

        [TestMethod]
        public void Json_extension_produces_json_content()
        {
            string content = ExportedColors.ToFileContent(".JSON");

            StringAssert.Contains(content, "\"color1\"");
            StringAssert.Contains(content, "\"#123456\"");
        }

        [TestMethod]
        public void Unsupported_extension_is_rejected_before_writing()
        {
            Assert.ThrowsException<InvalidOperationException>(() => ExportedColors.ToFileContent(".docx"));
        }
    }
}
