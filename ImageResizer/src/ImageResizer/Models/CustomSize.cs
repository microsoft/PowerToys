using ImageResizer.Properties;

namespace ImageResizer.Models
{
    public class CustomSize : ResizeSize
    {
        public override string Name
        {
            get => Resources.Input_Custom;
            set { /* no-op */ }
        }
    }
}
