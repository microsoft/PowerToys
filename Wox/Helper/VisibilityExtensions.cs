using System.Windows;

namespace Wox.Extensions
{
    public static class VisibilityExtensions
    {
        public static bool IsVisible(this Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }

        public static bool IsNotVisible(this Visibility visibility)
        {
            return !visibility.IsVisible();
        }
    }
}
