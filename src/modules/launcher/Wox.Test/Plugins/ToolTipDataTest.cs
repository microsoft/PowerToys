// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Plugin;

namespace Wox.Test.Plugins
{
    [TestClass]
    public class ToolTipDataTest
    {
        [TestMethod]
        public void ConstructorThrowsNullArgumentExceptionWhenToolTipTitleIsNull()
        {
            // Arrange
            string title = null;
            string text = "text";

            // Assert
            var ex = Assert.ThrowsException<ArgumentException>(() => new ToolTipData(title, text));
        }
    }
}
