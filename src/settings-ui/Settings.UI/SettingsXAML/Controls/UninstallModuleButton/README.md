# How add Uninstall Module Button to your Page.
1. Add this code to __Views/YourModule.xaml__
```xml
<custom:SettingsGroup>
    <custom:UninstallModuleButton ModuleName="YourModuleName"></custom:UninstallModuleButton>
</custom:SettingsGroup>
```
2. Next. Move your Content to Grid. And set for ```<custom:SettingsPageControl``` x:Name = "```IfUninstalledModule```". Add to __Views/YourModule.xaml__
```xml
<custom:NoModuleSection x:Name="NoModuleSection" ModuleName="YourModuleName"></custom:NoModuleSection>
```
3. Edit __Views/YourModule.xaml.cs__ and add to ```public YourModule()``` this code after ```this.InitializeComponent();```
```c#

List<string> deletedModules = UMBUtilites.ReadWordsFromFile("uninstalled_modules");
if (UMBUtilites.DoesListContainWord(deletedModules, "YourModuleName"))
{
    this.IfUninstalledModule.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    this.NoModuleSection.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
}

```