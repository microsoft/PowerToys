using PowerOCR.Utilities;
using System.Windows;

namespace PowerOCR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        WindowUtilities.LaunchOCROverlayOnEveryScreen();
    }
}
