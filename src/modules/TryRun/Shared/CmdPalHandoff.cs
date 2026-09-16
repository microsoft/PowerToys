// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PowerToys.TryRun.Launching;

// Shared by CmdPal and Try Run without referencing either application's execution engine.
public static partial class CmdPalHandoff
{
    public const string InputSwitch = "--cmdpal-stdin";
    public const string ApplicationName = "PowerToys.TryRun.exe";
    public const int MaximumCharacters = 100000;

    public static bool IsLocalPath(string? path) =>
        path is { Length: >= 3 and <= 4096 } && char.IsAsciiLetter(path[0]) &&
        path[1] == ':' && path[2] == '\\' && !path[3..].Contains(':') &&
        IsWellFormed(path) && !path.Any(char.IsControl) && path.IndexOfAny(['"', '<', '>', '|', '*', '?']) < 0;

    public static bool SupportsApplication(string path) => IsLocalPath(path) &&
        new[] { ".exe", ".ps1", ".cmd", ".bat" }.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static CmdPalSelection CreateSelection(string path, string commandLine = "")
    {
        if (!IsLocalPath(path) || commandLine.Length > 8192 || commandLine.Any(c => char.IsControl(c) && c != '\t'))
        {
            throw new ArgumentException("Choose a local file and use at most 8192 characters for its arguments.");
        }

        var arguments = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(commandLine))
        {
            // Parse Windows quoting; never ask the host shell to interpret the shortcut.
            var pointer = CommandLineToArgv("program.exe " + commandLine, out var count);
            if (pointer == IntPtr.Zero)
            {
                throw new IOException("Could not read the application's shortcut arguments.");
            }

            try
            {
                arguments = Enumerable.Range(1, count - 1)
                    .Select(index => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size))!)
                    .ToArray();
            }
            finally
            {
                LocalFree(pointer);
            }
        }

        var selection = new CmdPalSelection(System.IO.Path.GetFullPath(path), arguments);
        Validate(selection);
        return selection;
    }

    public static string Encode(CmdPalSelection selection)
    {
        Validate(selection);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Version", 1);
            writer.WriteString("Path", selection.Path);
            writer.WriteStartArray("Arguments");
            foreach (var argument in selection.Arguments)
            {
                writer.WriteStringValue(argument);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static CmdPalSelection Decode(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaximumCharacters)
        {
            throw new ArgumentException("The Command Palette selection is too large.");
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version) ||
                version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1 ||
                !root.TryGetProperty("Path", out var path) || path.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("Arguments", out var arguments) || arguments.ValueKind != JsonValueKind.Array || arguments.GetArrayLength() > 64 ||
                arguments.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String))
            {
                throw new ArgumentException("The Command Palette selection is missing or unsupported.");
            }

            var selection = new CmdPalSelection(path.GetString()!, arguments.EnumerateArray().Select(value => value.GetString()!).ToArray());
            Validate(selection);
            return selection;
        }
        catch (InvalidOperationException exception)
        {
            // JsonDocument permits an escaped unpaired surrogate until GetString.
            throw new ArgumentException("The Command Palette selection contains invalid text.", exception);
        }
    }

    public static async Task<CmdPalSelection> ReadAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[MaximumCharacters + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                return Decode(new string(buffer, 0, length));
            }

            length += count;
        }

        throw new ArgumentException("The Command Palette selection is too large.");
    }

    public static string FindApplication(string baseDirectory, string? configuredApplication)
    {
        if (!string.IsNullOrWhiteSpace(configuredApplication))
        {
            return ValidateApplication(configuredApplication);
        }

        // Installed layout, then the sibling output of the standalone development build.
        // Never search the current working directory, the selection's folder, or PATH.
        foreach (var directory in new[] { "TryRun", ".", @"..\..\TryRun", @"..\..\TryRun-Policies" })
        {
            var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, directory, ApplicationName));
            if (File.Exists(candidate))
            {
                return ValidateApplication(candidate);
            }
        }

        throw new FileNotFoundException("Build Try Run beside Command Palette, or set POWERTOYS_TRYRUN_APP to its executable and restart Command Palette.");
    }

    public static async Task<int> OpenAsync(string application, CmdPalSelection selection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        application = ValidateApplication(application);
        var payload = Encode(selection);
        if (!File.Exists(selection.Path) && !Directory.Exists(selection.Path))
        {
            throw new FileNotFoundException("The selected file or folder no longer exists.", selection.Path);
        }

        var start = new ProcessStartInfo(application)
        {
            UseShellExecute = false,
            WorkingDirectory = System.IO.Path.GetDirectoryName(application),
            RedirectStandardInput = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        start.ArgumentList.Add(InputSwitch);
        using var process = Process.Start(start) ?? throw new IOException("The Try Run window could not be started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await process.StandardInput.WriteAsync(payload.AsMemory(), timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        return process.Id;
    }

    private static string ValidateApplication(string application)
    {
        if (!IsLocalPath(application) || !System.IO.Path.GetFileName(application).Equals(ApplicationName, StringComparison.OrdinalIgnoreCase) || !File.Exists(application))
        {
            throw new FileNotFoundException("The configured Try Run application is unavailable. Build Try Run and check POWERTOYS_TRYRUN_APP.", application);
        }

        return System.IO.Path.GetFullPath(application);
    }

    private static void Validate(CmdPalSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!IsLocalPath(selection.Path) || selection.Arguments is null || selection.Arguments.Length > 64 ||
            selection.Arguments.Any(argument => argument is null || argument.Length > 4096 || !IsWellFormed(argument) || argument.Any(char.IsControl)) ||
            selection.Arguments.Sum(argument => argument.Length) > 8192 ||
            (selection.Arguments.Length > 0 && !SupportsApplication(selection.Path)))
        {
            throw new ArgumentException("Use a local file and at most 64 arguments (8192 characters total) for a Windows executable or script.");
        }
    }

    private static bool IsWellFormed(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsSurrogate(text[index]))
            {
                if (!char.IsHighSurrogate(text[index]) || index + 1 == text.Length || !char.IsLowSurrogate(text[++index]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    [LibraryImport("shell32.dll", EntryPoint = "CommandLineToArgvW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr CommandLineToArgv(string commandLine, out int count);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr LocalFree(IntPtr pointer);
}
