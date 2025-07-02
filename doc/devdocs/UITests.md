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
    <Project Sdk="Microsoft.NET.Sdk">
    <!-- Look at Directory.Build.props in root for common stuff as well -->
    <Import Project="..\..\..\Common.Dotnet.CsWinRT.props" />

    <PropertyGroup>
        <ProjectGuid>{4E0AE3A4-2EE0-44D7-A2D0-8769977254A0}</ProjectGuid>
        <RootNamespace>PowerToys.Hosts.UITests</RootNamespace>
        <AssemblyName>PowerToys.Hosts.UITests</AssemblyName>
        <IsPackable>false</IsPackable>
        <IsTestProject>true</IsTestProject>
        <Nullable>enable</Nullable>
        <OutputType>Library</OutputType>

        <!-- This is a UI test, so don't run as part of MSBuild -->
        <RunVSTest>false</RunVSTest>
        </PropertyGroup>
        <PropertyGroup>
        <OutputPath>$(SolutionDir)$(Platform)\$(Configuration)\tests\Hosts.UITests\</OutputPath>
        </PropertyGroup>

        <ItemGroup>
        <PackageReference Include="MSTest" />
        <ProjectReference Include="..\..\..\common\UITestAutomation\UITestAutomation.csproj" />
        </ItemGroup>
    </Project>

  ```
- Inherit your test class from UITestBase.
  >Set Scope: The default scope starts from the PowerToys settings UI. If you want to start from your own module, set the constructor as shown below:
  
  >Specify Scope:
  ```
    [TestClass]
    public class HostModuleTests : UITestBase
    {
        public HostModuleTests()
            : base(PowerToysModule.Hosts, WindowSize.Small_Vertical)
        {
        }
    }
  ```

- Then you can start performing the UI operations.

**Example**
```
[TestMethod("Hosts.Basic.EmptyViewShouldWork")]
[TestCategory("Hosts File Editor #4")]
public void TestEmptyView()
{
    this.CloseWarningDialog();
    this.RemoveAllEntries();

    // 'Add an entry' button (only show-up when list is empty) should be visible
    Assert.IsTrue(this.HasOne<HyperlinkButton>("Add an entry"), "'Add an entry' button should be visible in the empty view");

    VisualAssert.AreEqual(this.TestContext, this.Find("Entries"), "EmptyView");

    // Click 'Add an entry' from empty-view for adding Host override rule
    this.Find<HyperlinkButton>("Add an entry").Click();

    this.AddEntry("192.168.0.1", "localhost", false, false);

    // Should have one row now and not more empty view
    Assert.IsTrue(this.Has<Button>("Delete"), "Should have one row now");
    Assert.IsFalse(this.Has<HyperlinkButton>("Add an entry"), "'Add an entry' button should be invisible if not empty view");

    VisualAssert.AreEqual(this.TestContext, this.Find("Entries"), "NonEmptyView");
}
```

## Extra tools and information

 **Accessibility Tools**:
While working on tests, you may need a tool that helps you to view the element's accessibility data, e.g. for finding the button to click. For this purpose, you could use [AccessibilityInsights](https://accessibilityinsights.io/docs/windows/overview).
