namespace WIC
{
    public struct Resolution
    {
        public Resolution(double dpiX, double dpiY)
        {
            DpiX = dpiX;
            DpiY = dpiY;
        }

        public double DpiX { get; }
        public double DpiY { get; }
    }
}
