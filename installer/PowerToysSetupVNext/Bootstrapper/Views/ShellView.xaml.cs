using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Bootstrapper.Views
{
  public partial class ShellView
  {
    public ShellView()
    {
      InitializeComponent();
      try
      {
        BitmapSource applyIconSource = null;
        BitmapSource repairIconSource = null;

        try
        {
          applyIconSource = Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Shield.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
          repairIconSource = Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Shield.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
          // ignored
        }

        if (applyIconSource != null)
          ApplyShieldIcon.Source = applyIconSource;

        if (repairIconSource != null)
          RepairShieldIcon.Source = repairIconSource;
      }
      catch
      {
        // ignore
      }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      DragMove();
    }
  }
}