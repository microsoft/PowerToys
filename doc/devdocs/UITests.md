# UI tests framework

 A specialized UI test framework for PowerToys that makes it easy to write UI tests for PowerToys modules or settings. Let's start writing UI tests!

## Before running tests  

- Install Windows Application Driver v1.2.1 from https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1 to the default directory (`C:\Program Files (x86)\Windows Application Driver`)

- Enable Developer Mode in Windows settings

## Running tests

- Exit PowerToys if it's running.

- Open `PowerToys.sln` in Visual Studio and build the solution.

- Run tests in the Test Explorer (`Test > Test Explorer` or `Ctrl+E, T`).


## How to add the first UI tests for your modules

- Create a new project and add the following references to the project file. Change the OutputPath to your own module's path.
  ```
  	<PropertyGroup>
  		<OutputType>Library</OutputType>
  		<!-- This is a UI test, so don't run as part of MSBuild -->
  		<RunVSTest>false</RunVSTest>
  	</PropertyGroup>
  
  	<PropertyGroup>
  		<OutputPath>..\..\..\..\$(Platform)\$(Configuration)\tests\KeyboardManagerUITests\</OutputPath>
  	</PropertyGroup>
  
  	<ItemGroup>
  	    <PackageReference Include="MSTest" />
  	    <ProjectReference Include="..\..\..\common\UITestAutomation\UITestAutomation.csproj" />
  	    <Folder Include="Properties\" />
	</ItemGroup>
  ```
- Inherit your test class from UITestBase.
  >Set Scope: The default scope starts from the PowerToys settings UI. If you want to start from your own module, set the constructor as shown below:
  
- >Default Scope:
  ```
  [TestClass]
  public class RunFancyZonesTest : UITestBase
  {
      public RunFancyZonesTest()
          : base()
      {
      }
  }
  ```
  >Specify Scope:
  ```
  [TestClass]
  public class RunFancyZonesTest : UITestBase
  {
      public RunFancyZonesTest()
          : base(PowerToysModule.FancyZone)
      {
      }
  }
  ```

- Then you can start using session to perform the UI operations.

**Example**
```
namespace UITests_KeyboardManager
{
    [TestClass]
    public class RunKeyboardManagerUITests : UITestBase
    {
        [TestMethod]
        public void OpenKeyboardManagerEditor()
        {
            // Open KeyboardManagerEditor
            Session.FindElement<Button>(By.Name("Remap a key")).Click();
            Session.Attach(PowerToysModuleWindow.KeyboardManagerKeys);

            // Maximize window
            var window = Session.FindElementByName<Window>("Remap keys").Maximize();

            // Click button
            Session.FindElementByName<Button>("Add key remapping").Click();
            Session.FindElementByName<Element>("Row 1, Select:").FindElementByName<Button>("Select").Click();
            Session.FindElementByName<Window>("Select a key on selected keyboard").FindElementByName<Button>("Cancel").Click();
            window.Close();

            // Back to Settings
            Session.Attach(PowerToysModuleWindow.PowerToysSettings);
        }
    }
}
```

## Extra tools and information

 **Accessibility Tools**:
While working on tests, you may need a tool that helps you to view the element's accessibility data, e.g. for finding the button to click. For this purpose, you could use [AccessibilityInsights](https://accessibilityinsights.io/docs/windows/overview) 