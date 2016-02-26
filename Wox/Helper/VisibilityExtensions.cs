using System.Windows;

namespace Wox.Helper
{
    public static class VisibilityExtensions
    {
        public static bool IsVisible(this Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
    }
}
