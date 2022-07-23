using PowerOCR.Keyboard;
using PowerOCR.Settings;
using PowerOCR.Utilities;
using System.Windows;

namespace PowerOCR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    KeyboardMonitor? keyboardMonitor;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // WindowUtilities.LaunchOCROverlayOnEveryScreen();
        UserSettings userSettings = new();
        keyboardMonitor = new(userSettings);
        keyboardMonitor?.Start();
    }
}
