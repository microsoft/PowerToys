namespace System.Windows.Media.Imaging
{
    static class BitmapEncoderExtensions
    {
        public static bool CanEncode(this BitmapEncoder encoder)
        {
            try
            {
                var _ = encoder.CodecInfo;
            }
            catch (NotSupportedException)
            {
                return false;
            }

            return true;
        }
    }
}
