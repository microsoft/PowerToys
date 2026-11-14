param(
    [string]$TestDirectory = "x64\Debug\WinUI3Apps\FileConverterSmokeTest",
    [string]$InputFileName = "sample.bmp",
    [string]$ExpectedOutputFileName = "sample_converted.png",
    [string]$VerbName = "Convert to...",
    [int]$InvokeTimeoutMs = 20000,
    [int]$OutputWaitTimeoutMs = 10000
)

$ErrorActionPreference = "Stop"

$resolvedTestDir = (Resolve-Path $TestDirectory).Path
$outputPath = Join-Path $resolvedTestDir $ExpectedOutputFileName
if (Test-Path $outputPath)
{
    Remove-Item $outputPath -Force
}

$code = @"
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

public static class ShellVerbRunner
{
    public static string Invoke(string directoryPath, string fileName, string targetVerb, int timeoutMs)
    {
        string result = "Unknown";
        Exception error = null;
        bool completed = false;

        Thread thread = new Thread(() =>
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                object shell = Activator.CreateInstance(shellType);
                object folder = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { directoryPath });
                if (folder == null)
                {
                    result = "Folder not found";
                    return;
                }

                Type folderType = folder.GetType();
                object item = folderType.InvokeMember("ParseName", BindingFlags.InvokeMethod, null, folder, new object[] { fileName });
                if (item == null)
                {
                    result = "Item not found";
                    return;
                }

                Type itemType = item.GetType();
                object verbs = itemType.InvokeMember("Verbs", BindingFlags.InvokeMethod, null, item, null);
                Type verbsType = verbs.GetType();
                int count = (int)verbsType.InvokeMember("Count", BindingFlags.GetProperty, null, verbs, null);

                for (int index = 0; index < count; index++)
                {
                    object verb = verbsType.InvokeMember("Item", BindingFlags.InvokeMethod, null, verbs, new object[] { index });
                    if (verb == null)
                    {
                        continue;
                    }

                    Type verbType = verb.GetType();
                    string name = (verbType.InvokeMember("Name", BindingFlags.GetProperty, null, verb, null) as string ?? string.Empty)
                        .Replace("&", string.Empty)
                        .Trim();

                    if (!string.Equals(name, targetVerb, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    verbType.InvokeMember("DoIt", BindingFlags.InvokeMethod, null, verb, null);
                    result = "Invoked";
                    return;
                }

                result = "Verb not found";
            }
            catch (Exception ex)
            {
                Exception current = ex;
                string details = string.Empty;
                while (current != null)
                {
                    details += current.GetType().FullName + ": " + current.Message + Environment.NewLine;
                    current = current.InnerException;
                }

                error = new Exception(details.Trim());
            }
            finally
            {
                completed = true;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(timeoutMs);

        if (!completed)
        {
            return "Timeout";
        }

        if (error != null)
        {
            return "Error: " + error.Message;
        }

        return result;
    }
}

[ComImport, Guid("A08CE4D0-FA25-44AB-B57C-C7B1C323E0B9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IExplorerCommand
{
    int GetTitle(IShellItemArray psiItemArray, out IntPtr ppszName);
    int GetIcon(IShellItemArray psiItemArray, out IntPtr ppszIcon);
    int GetToolTip(IShellItemArray psiItemArray, out IntPtr ppszInfotip);
    int GetCanonicalName(out Guid pguidCommandName);
    int GetState(IShellItemArray psiItemArray, int fOkToBeSlow, out uint pCmdState);
    int Invoke(IShellItemArray psiItemArray, [MarshalAs(UnmanagedType.Interface)] object pbc);
    int GetFlags(out uint pFlags);
    int EnumSubCommands(out IEnumExplorerCommand ppEnum);
}

[ComImport, Guid("A88826F8-186F-4987-AADE-EA0CEF8FBFE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IEnumExplorerCommand
{
    int Next(uint celt, out IExplorerCommand pUICommand, out uint pceltFetched);
    int Skip(uint celt);
    int Reset();
    int Clone(out IEnumExplorerCommand ppenum);
}

[ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItemArray
{
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItem
{
}

public static class FileConverterExplorerCommandRunner
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem ppv);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHCreateShellItemArrayFromShellItem(IShellItem psi, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemArray ppv);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    private static string NormalizeLabel(string value)
    {
        return (value ?? string.Empty).Replace("&", string.Empty).Trim();
    }

    public static string InvokeBySubCommand(string inputFilePath, string targetSubCommandLabel, int timeoutMs)
    {
        string result = "Unknown";
        Exception error = null;
        bool completed = false;

        Thread thread = new Thread(() =>
        {
            try
            {
                Guid shellItemGuid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
                int hr = SHCreateItemFromParsingName(inputFilePath, IntPtr.Zero, ref shellItemGuid, out IShellItem shellItem);
                if (hr < 0)
                {
                    result = "SHCreateItemFromParsingName failed: 0x" + hr.ToString("X8");
                    return;
                }

                Guid shellArrayGuid = new Guid("B63EA76D-1F85-456F-A19C-48159EFA858B");
                hr = SHCreateShellItemArrayFromShellItem(shellItem, ref shellArrayGuid, out IShellItemArray selection);
                if (hr < 0)
                {
                    result = "SHCreateShellItemArrayFromShellItem failed: 0x" + hr.ToString("X8");
                    return;
                }

                Type commandType = Type.GetTypeFromCLSID(new Guid("57EC18F5-24D5-4DC6-AE2E-9D0F7A39F8BA"), true);
                IExplorerCommand root = (IExplorerCommand)Activator.CreateInstance(commandType);

                hr = root.EnumSubCommands(out IEnumExplorerCommand enumCommands);
                if (hr < 0 || enumCommands == null)
                {
                    result = "EnumSubCommands failed: 0x" + hr.ToString("X8");
                    return;
                }

                string expected = NormalizeLabel(targetSubCommandLabel);
                bool requireMatch = !string.IsNullOrWhiteSpace(expected);

                while (true)
                {
                    hr = enumCommands.Next(1, out IExplorerCommand command, out uint fetched);
                    if (fetched == 0 || command == null)
                    {
                        result = "Subcommand not found";
                        return;
                    }

                    IntPtr titlePtr = IntPtr.Zero;
                    string title = string.Empty;
                    int titleHr = command.GetTitle(selection, out titlePtr);
                    if (titleHr >= 0 && titlePtr != IntPtr.Zero)
                    {
                        title = Marshal.PtrToStringUni(titlePtr) ?? string.Empty;
                        CoTaskMemFree(titlePtr);
                    }

                    string normalizedTitle = NormalizeLabel(title);
                    if (requireMatch && !string.Equals(normalizedTitle, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    hr = command.Invoke(selection, null);
                    result = hr < 0 ? ("Invoke failed: 0x" + hr.ToString("X8")) : "Invoked";
                    return;
                }
            }
            catch (Exception ex)
            {
                Exception current = ex;
                string details = string.Empty;
                while (current != null)
                {
                    details += current.GetType().FullName + ": " + current.Message + Environment.NewLine;
                    current = current.InnerException;
                }

                error = new Exception(details.Trim());
            }
            finally
            {
                completed = true;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(timeoutMs);

        if (!completed)
        {
            return "Timeout";
        }

        if (error != null)
        {
            return "Error: " + error.Message;
        }

        return result;
    }
}
"@

Add-Type -TypeDefinition $code -Language CSharp
function Resolve-TargetSubCommandLabel([string]$ExpectedOutputName, [string]$RequestedVerb)
{
    if (-not [string]::IsNullOrWhiteSpace($RequestedVerb) -and $RequestedVerb -ne "Convert to...")
    {
        return $RequestedVerb
    }

    $extension = [System.IO.Path]::GetExtension($ExpectedOutputName).ToLowerInvariant()
    switch ($extension)
    {
        ".png" { return "PNG" }
        ".jpg" { return "JPG" }
        ".jpeg" { return "JPEG" }
        ".bmp" { return "BMP" }
        ".tif" { return "TIFF" }
        ".tiff" { return "TIFF" }
        ".heic" { return "HEIC" }
        ".heif" { return "HEIF" }
        ".webp" { return "WebP" }
        default { return "PNG" }
    }
}

$invokeResult = [ShellVerbRunner]::Invoke($resolvedTestDir, $InputFileName, $VerbName, $InvokeTimeoutMs)
Write-Host "Invoke result: $invokeResult"

if ($invokeResult -eq "Verb not found")
{
    $inputPath = Join-Path $resolvedTestDir $InputFileName
    $subCommandLabel = Resolve-TargetSubCommandLabel -ExpectedOutputName $ExpectedOutputFileName -RequestedVerb $VerbName
    Write-Host "Shell verb fallback: trying IExplorerCommand subcommand '$subCommandLabel'"
    $invokeResult = [FileConverterExplorerCommandRunner]::InvokeBySubCommand($inputPath, $subCommandLabel, $InvokeTimeoutMs)
    Write-Host "Fallback invoke result: $invokeResult"
}

if ($invokeResult -ne "Invoked")
{
    throw "Verb invocation failed: $invokeResult"
}

$waited = 0
$step = 250
while ($waited -lt $OutputWaitTimeoutMs -and -not (Test-Path $outputPath))
{
    Start-Sleep -Milliseconds $step
    $waited += $step
}

if (-not (Test-Path $outputPath))
{
    throw "Output file was not created: $outputPath"
}

$item = Get-Item $outputPath
Write-Host "Created: $($item.FullName)"
Write-Host "Size: $($item.Length)"
