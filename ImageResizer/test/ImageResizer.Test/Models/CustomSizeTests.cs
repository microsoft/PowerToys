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
