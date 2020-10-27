# XAML Island Tweaks
Few tweaks were made to fix issues with Xaml Islands. These tweaks should be removed after migrating to WINUI3. The tweaks are listed below: 
1. Workaround to ensure XAML Island application terminates if attempted to close from taskbar while minimized:
```
private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
{
    isOpen = false;

    // XAML Islands: If the window is closed while minimized, exit the process. Required to avoid process not terminating issue - https://github.com/microsoft/PowerToys/issues/4430
    if (WindowState == WindowState.Minimized)
    {
        // Run Environment.Exit on a separate task to avoid performance impact
        System.Threading.Tasks.Task.Run(() => { Environment.Exit(0); });
    }
}
```
2. Workaround to hide the XAML Island blank icon in the taskbar when the XAML Island application is loading:
```
var coreWindow = Windows.UI.Core.CoreWindow.GetForCurrentThread(); 
var coreWindowInterop = Interop.GetInterop(coreWindow); 
Interop.ShowWindow(coreWindowInterop.WindowHandle, Interop.SW_HIDE); 
```
3. Workaround to prevent XAML Island failing to render on Nvidia workstation graphics cards:
```
 // XAML Islands: If the window is open, explicitly force it to be shown to solve the blank dialog issue https://github.com/microsoft/PowerToys/issues/3384 
 if (isOpen) 
 { 
     Show(); 
 } 
 ```
