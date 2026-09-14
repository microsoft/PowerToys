// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;

namespace PowerToys.TryRun.Core;

public static class ExplorerRegistration
{
    public const string CommandClassId = "D2B3BC02-CFF8-4A26-94B4-62441A1FF659";
    public const string VerbName = "PowerToys.TryRun.Experimental";
    private const string Owner = "PowerToys.TryRun.Explorer.v1";
    private const string OwnerValue = "TryRunOwner";
    private static readonly string ClassKey = $"CLSID\\{{{CommandClassId}}}";
    private static readonly string ApplicationKey = $"AppID\\{{{CommandClassId}}}";
    private static readonly string[] VerbKeys = [$"*\\shell\\{VerbName}", $"Directory\\shell\\{VerbName}"];

    public static string GetResultPath(string directory, string correlationId)
    {
        if (!Guid.TryParseExact(correlationId, "N", out var identifier))
        {
            throw new ArgumentException("A registration receipt requires a valid operation identifier.");
        }

        return Path.Combine(WorkspacePath.LocalPath(directory), $"TryRun-registration-{identifier:N}.json");
    }

    // The caller supplies HKCU\Software\Classes in production. Tests use a
    // disposable private key so they never change the Explorer integration.
    public static void Register(RegistryKey classes, string application, string broker)
    {
        ArgumentNullException.ThrowIfNull(classes);
        application = RuntimeFile.Resolve(application);
        broker = RuntimeFile.Resolve(broker);
        if (!Path.GetFileName(application).Equals("PowerToys.TryRun.exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(broker).Equals("PowerToys.TryRun.Explorer.exe", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetDirectoryName(broker)!.Equals(Path.Combine(Path.GetDirectoryName(application)!, "Explorer"), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Register the Try Run application and its adjacent Explorer helper.");
        }

        foreach (var path in VerbKeys.Prepend(ClassKey).Prepend(ApplicationKey))
        {
            EnsureOwnedOrMissing(classes, path);
        }

        using (var key = classes.CreateSubKey(ClassKey))
        {
            key.SetValue(OwnerValue, Owner);
            key.SetValue(string.Empty, "Try Run Explorer selection (experimental)");
            key.SetValue("AppID", $"{{{CommandClassId}}}");
            using var server = key.CreateSubKey("LocalServer32");
            server.SetValue(string.Empty, CommandEncoding.WindowsArgument(broker));
            server.SetValue("ServerExecutable", broker);
        }

        using (var key = classes.CreateSubKey(ApplicationKey))
        {
            key.SetValue(OwnerValue, Owner);
            key.SetValue(string.Empty, "Try Run Explorer selection (experimental)");
        }

        foreach (var path in VerbKeys)
        {
            using var verb = classes.CreateSubKey(path);
            verb.SetValue(OwnerValue, Owner);
            verb.SetValue(string.Empty, "Try Run");
            verb.SetValue("MUIVerb", "Try Run");
            verb.SetValue("Icon", CommandEncoding.WindowsArgument(application) + ",0");
            verb.SetValue("MultiSelectModel", "Player");
            verb.SetValue("NeverDefault", string.Empty);
            using var command = verb.CreateSubKey("command");
            command.DeleteValue(string.Empty, throwOnMissingValue: false);
            command.SetValue("DelegateExecute", $"{{{CommandClassId}}}");
        }
    }

    public static void Unregister(RegistryKey classes)
    {
        ArgumentNullException.ThrowIfNull(classes);
        foreach (var path in VerbKeys.Append(ClassKey).Append(ApplicationKey))
        {
            EnsureOwnedOrMissing(classes, path);
        }

        foreach (var path in VerbKeys.Append(ClassKey).Append(ApplicationKey))
        {
            classes.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
    }

    public static bool IsRegistered(RegistryKey classes, string broker)
    {
        ArgumentNullException.ThrowIfNull(classes);
        using var server = classes.OpenSubKey(ClassKey + "\\LocalServer32");
        return string.Equals(server?.GetValue("ServerExecutable") as string, broker, StringComparison.OrdinalIgnoreCase) && VerbKeys.All(path =>
        {
            using var key = classes.OpenSubKey(path + "\\command");
            return string.Equals(key?.GetValue("DelegateExecute") as string, $"{{{CommandClassId}}}", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static void EnsureOwnedOrMissing(RegistryKey classes, string path)
    {
        using var key = classes.OpenSubKey(path);
        if (key is not null && !Equals(key.GetValue(OwnerValue), Owner))
        {
            throw new InvalidOperationException($"Registration was not changed because this key is not owned by Try Run: {path}");
        }
    }
}
