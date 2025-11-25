Build a packaged Command Palette extension that exposes com.microsoft.commandpalette, while re-using the existing PowerToys sparse MSIX identity for discovery.
The extension directly references and consumes PowerToys module libraries (C#) to execute commands, removing the need for inter-process communication (IPC) with the PowerToys Runner for these interactions.

0) Goals / Non-Goals

Goals

Let Command Palette (host) discover a PowerToys extension via the Windows AppExtension contract.

Provide direct, low-latency execution of PowerToys commands by hosting module logic directly within the Extension process.

Define a pattern for exposing PowerToys modules as consumable C# libraries.

Non-Goals

Not a general inter-process broker.

Not relying on the PowerToys Runner process to be active for command execution (where possible).

1) Terms

Host: Command Palette process (WinUI3 app) loading extensions.

Extension: sparse packaged project that declares uap3:AppExtension Name="com.microsoft.commandpalette".

Module Library: A .NET assembly (DLL) implementing the core logic of a PowerToys module, exposing a public C# API.

Command: A unit of invocation (e.g., workspace.list, awake.enable) mapped directly to a C# method call.

2) High-Level Architecture

1. Command Palette gathers user intent → Invokes Command Provider in Extension.

2. Extension (running in CmdPal process) calls into the referenced Module Library.

   ```csharp
   // Direct C# call
   var workspaces = _workspacesService.GetWorkspaces();
   ```

3. Module Library executes logic (e.g., reads config, spawns process, applies setting).

4. Result is returned directly to the Extension and rendered by Command Palette.

3) Prerequisites

PowerToys sparse package installed.

Module Libraries refactored/available as .NET assemblies (e.g., `PowerToys.Workspaces.Lib.dll`).

4) Packaging & Identity
4.1 Add your executable to the sparse package

* Edit src/PackageIdentity/AppxManifest.xml:
```xml
<Applications>
  <Application Id="CmdPalExt.YourExtension"
               Executable="External\Win32\<yourExe>.exe"
               EntryPoint="Windows.FullTrustApplication">
    <uap:VisualElements DisplayName="Your CmdPal Extension" Square44x44Logo="Assets\Square44x44Logo.png" Description="..." />
  </Application>
  <!-- Existing PowerToys entries remain -->
</Applications>
```

* Embed sparse identity in your Win32 binary (RT_MANIFEST):
```xml
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <msix xmlns="urn:schemas-microsoft-com:msix.v1"
        packageName="Microsoft.PowerToys.SparseApp"
        applicationId="CmdPalExt.YourExtension"
        publisher="CN=PowerToys Dev Cert, O=Microsoft Corporation, L=..., C=US"/>
</assembly>
```

4.2 Register the App Extension & COM server
In your packaged project’s Package.appxmanifest (follow SamplePagesExtension pattern):
```xml
<Extensions>
  <!-- COM host out-of-proc for the extension -->
  <com:Extension Category="windows.comServer">
    <com:ComServer>
      <com:ExeServer Executable="External\Win32\<yourExe>.exe"
                      Arguments="-RegisterProcessAsComServer"
                      DisplayName="CmdPal Extension COM Host">
        <com:Class Id="{YOUR-CLS-GUID}" DisplayName="YourExtension" />
      </com:ExeServer>
    </com:ComServer>
  </com:Extension>

  <!-- Command Palette AppExtension -->
  <uap3:Extension Category="windows.appExtension">
    <uap3:AppExtension Name="com.microsoft.commandpalette"
                       Id="YourExtension"
                       PublicFolder="Public">
      <uap3:Properties>
        <CmdPalProvider xmlns="http://schemas.microsoft.com/commandpalette/2024/extension">
          <Metadata DisplayName="Your Extension" Description="..." />
          <Activation>
            <CreateInstance ClassId="{YOUR-CLS-GUID}" />
          </Activation>
        </CmdPalProvider>
      </uap3:Properties>
    </uap3:AppExtension>
  </uap3:Extension>
</Extensions>
```

4.3 Project configuration
```xml
<PropertyGroup>
  <GenerateAppxPackageOnBuild>true</GenerateAppxPackageOnBuild>
  <AppxBundle>Never</AppxBundle>
  <ApplicationManifest>..\..\..\src\PackageIdentity\AppxManifest.xml</ApplicationManifest>
  <OutDir>$(SolutionDir)$(Platform)\$(Configuration)\WinUI3Apps\CmdPalExtensions\$(MSBuildProjectName)\</OutDir>
</PropertyGroup>
```

5) Extension Entry Point (COM) & Lifetime
5.1 COM entry
```csharp
[ComVisible(true)]
[Guid("YOUR-CLS-GUID")]
public sealed class SampleExtension : IExtension
{
    private readonly ManualResetEvent _lifetime = new(false);

    public void Initialize(IHostContext ctx)
    {
        // optional host features / events subscription
    }

    public IEnumerable<ICommandProvider> GetProviders() =>
        new ICommandProvider[] { new PowerToysCommandProvider() };

    public void Dispose()
    {
        _lifetime.Set(); // signal process shutdown
    }

    // Program.cs
    public static int Main(string[] args)
    {
        if (args.Contains("-RegisterProcessAsComServer"))
        {
            using var server = ExtensionServer.RegisterExtension<SampleExtension>();
            WaitHandle.WaitAny(new[] { server.LifetimeHandle /* or your own */ });
            return 0;
        }
        return 0;
    }
}
```
6) Providers, Pages, and Items
Derive from Microsoft.CommandPalette.Extensions.Toolkit.CommandProvider.

Provide a consistent capabilities set:
```csharp
public sealed class PowerToysCommandProvider : CommandProvider
{
    public PowerToysCommandProvider()
    {
        Id = "PowerToys";
        DisplayName = "PowerToys";
        Icon = new Uri("ms-appx:///Public/Icons/powertoys.png");
    }

    public override IEnumerable<CommandItem> TopLevelCommands()
    {
        yield return CommandItem.Run("Workspaces", "List or launch a workspace")
             .WithId("workspace.list")
             .WithInvoke(async ctx => {
                 // Direct call to module library
                 var workspaces = new WorkspacesService();
                 var list = workspaces.GetWorkspaces();
                 // ...
             });
        // …more commands
    }
}
```
Use toolkit pages (ListPage, MarkdownContent, FormContent, etc.) to render results.

7) Module Library Pattern
To expose a module to Command Palette, the module must provide a .NET-consumable library (C# Project or C++/CLI wrapper).

7.1 Library Responsibilities
*   **Statelessness:** The library should ideally be stateless or manage state via persistent storage (files, registry) that can be shared between the Runner and the Extension.
*   **Public API:** Expose high-level methods for commands (e.g., `Launch()`, `Enable()`, `GetState()`).
*   **Dependencies:** Keep dependencies minimal to avoid bloating the Extension package.

7.2 Interface Definition (Recommended)
Define an interface for your module's capabilities to allow for easy testing and mocking.

```csharp
public interface IWorkspacesService
{
    IEnumerable<Workspace> GetWorkspaces();
    void LaunchWorkspace(string workspaceId);
    void LaunchEditor();
}
```

8) Example: Workspaces Module
The Workspaces module exposes a C# library `PowerToys.Workspaces.Lib` that implements the logic.

8.1 Implementation (in `src/modules/Workspaces/WorkspacesLib`)
```csharp
public class WorkspacesService : IWorkspacesService
{
    public IEnumerable<Workspace> GetWorkspaces()
    {
        // Read from settings/storage
        return WorkspaceStorage.Load();
    }

    public void LaunchWorkspace(string workspaceId)
    {
        // Logic to launch apps defined in the workspace
        var workspace = WorkspaceStorage.Get(workspaceId);
        Launcher.Launch(workspace);
    }

    public void LaunchEditor()
    {
        // Launch the editor executable
        Process.Start("PowerToys.Workspaces.Editor.exe");
    }
}
```

8.2 Consumption (in `Microsoft.CmdPal.Ext.PowerToys`)
The extension project references `PowerToys.Workspaces.Lib.csproj`.

```csharp
public sealed class PowerToysCommandProvider : CommandProvider
{
    private readonly IWorkspacesService _workspaces = new WorkspacesService();

    public override IEnumerable<CommandItem> TopLevelCommands()
    {
        yield return CommandItem.Run("Workspaces", "List or launch a workspace")
             .WithId("workspace.list")
             .WithInvoke(async ctx => {
                 var list = _workspaces.GetWorkspaces();
                 // Render list page...
             });
    }
}
```

9) Security & Isolation
*   **Process Identity:** The Extension runs as the user in the Command Palette process (or a separate extension process). It inherits the user's permissions.
*   **Elevation:** If a module requires elevation (e.g., modifying system files), the Library must handle the UAC prompt or fail gracefully. The Extension cannot "request" elevation from the Runner via this direct path.
*   **Concurrency:** Be aware that `PowerToys.exe` (Runner) and `CmdPal.exe` (Extension) may access shared resources (settings files) simultaneously. Use appropriate file locking or synchronization.

10) Telemetry
*   The Extension should log telemetry events directly using the standard PowerToys telemetry pipeline, ensuring the `Caller` is identified as the Command Palette Extension.
