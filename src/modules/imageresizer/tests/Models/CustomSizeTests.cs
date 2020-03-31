// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using ImageResizer.Properties;
using Xunit;

namespace ImageResizer.Models
{
    public class CustomSizeTests
    {
        [Fact]
        public void Name_works()
        {
            var size = new CustomSize();

            size.Name = "Ignored";

            Assert.Equal(Resources.Input_Custom, size.Name);
        }
    }
}
