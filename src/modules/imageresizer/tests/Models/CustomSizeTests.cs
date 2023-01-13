// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Models
{
    [TestClass]
    public class CustomSizeTests
    {
        [TestMethod]
        public void NameWorks()
        {
            var size = new CustomSize
            {
                Name = "Ignored",
            };

            Assert.AreEqual(Resources.Input_Custom, size.Name);
        }
    }
}
