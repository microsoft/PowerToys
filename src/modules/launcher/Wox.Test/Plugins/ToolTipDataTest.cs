// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using NUnit.Framework;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestFixture]
    internal class ToolTipDataTest
    {
        [Test]
        public void Constructor_ThrowsNullArgumentException_WhenToolTipTitleIsNull()
        {
            // Arrange
            string title = null;
            string text = "text";

            // Assert
            var ex = Assert.Throws<ArgumentException>(() => new ToolTipData(title, text));
        }
    }
}
