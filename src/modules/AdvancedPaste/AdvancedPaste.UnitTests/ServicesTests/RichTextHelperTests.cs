// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.UnitTests.ServicesTests
{
    [TestClass]
    public class RichTextHelperTests
    {
        [TestMethod]
        public async Task ToRichTextAsync_ConvertsMarkdownToHtml()
        {
            var markdown = "**Bold** and *italic* text";
            var package = new DataPackage();
            package.SetText(markdown);
            var view = package.GetView();

            var html = await RichTextHelper.ToRichTextAsync(view);

            Assert.IsTrue(html.Contains("<strong>Bold</strong>"), "Missing bold tag");
            Assert.IsTrue(html.Contains("<em>italic</em>"), "Missing italic tag");
        }

        [TestMethod]
        public async Task ToRichTextAsync_ReturnsEmptyString_WhenEmpty()
        {
            var package = new DataPackage();
            package.SetText(string.Empty);
            var view = package.GetView();

            var html = await RichTextHelper.ToRichTextAsync(view);

            Assert.AreEqual(string.Empty, html);
        }
    }
}
