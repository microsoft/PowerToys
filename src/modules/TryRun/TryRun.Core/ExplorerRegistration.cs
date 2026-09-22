// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace PowerToys.TryRun.Core;

public static class ExplorerRegistration
{
    public const string CommandClassId = "D2B3BC02-CFF8-4A26-94B4-62441A1FF659";
    public const string VerbName = "PowerToys.TryRun.Experimental";
    private const string Owner = "PowerToys.TryRun.Explorer.v1";
    private const string OwnerValue = "TryRunOwner";
    private const string BrokerValue = "TryRunBroker";
    private static readonly string ClassKey = $"CLSID\\{{{CommandClassId}}}";
    private static readonly string ApplicationKey = $"AppID\\{{{CommandClassId}}}";
    private static readonly string[] VerbKeys = [$"*\\shell\\{VerbName}", $"Directory\\shell\\{VerbName}"];

    public static string GetRegistrationMutexName(RegistryKey classes)
    {
        ArgumentNullException.ThrowIfNull(classes);
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User?.Value ?? throw new InvalidOperationException("Could not identify the current user for Explorer registration.");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sid + "\n" + classes.Name.ToUpperInvariant()));

        // HKCU can be shared across desktop sessions. The user SID and registry
        // root separate users and private test fixtures within the global namespace.
        return "Global\\PowerToys.TryRun.ExplorerRegistration." + Convert.ToHexStringLower(hash);
    }

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

        using var registration = new RegistrationLock(GetRegistrationMutexName(classes));
        foreach (var path in VerbKeys.Prepend(ClassKey).Prepend(ApplicationKey))
        {
            EnsureOwnedOrMissing(classes, path);
        }

        using (var key = classes.CreateSubKey(ClassKey))
        {
            key.SetValue(OwnerValue, Owner);
            key.SetValue(BrokerValue, broker);
            key.SetValue(string.Empty, "Try Run Explorer selection (experimental)");
            key.SetValue("AppID", $"{{{CommandClassId}}}");
            using var server = key.CreateSubKey("LocalServer32");
            server.SetValue(string.Empty, CommandEncoding.WindowsArgument(broker));
            server.SetValue("ServerExecutable", broker);
        }

        using (var key = classes.CreateSubKey(ApplicationKey))
        {
            key.SetValue(OwnerValue, Owner);
            key.SetValue(BrokerValue, broker);
            key.SetValue(string.Empty, "Try Run Explorer selection (experimental)");
        }

        foreach (var path in VerbKeys)
        {
            using var verb = classes.CreateSubKey(path);
            verb.SetValue(OwnerValue, Owner);
            verb.SetValue(BrokerValue, broker);
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
        using var registration = new RegistrationLock(GetRegistrationMutexName(classes));
        foreach (var path in VerbKeys.Append(ClassKey).Append(ApplicationKey))
        {
            EnsureOwnedOrMissing(classes, path);
        }

        foreach (var path in VerbKeys.Append(ClassKey).Append(ApplicationKey))
        {
            classes.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
    }

    public static void Unregister(RegistryKey classes, string expectedBroker)
    {
        ArgumentNullException.ThrowIfNull(classes);
        expectedBroker = WorkspacePath.LocalPath(expectedBroker);
        using var registration = new RegistrationLock(GetRegistrationMutexName(classes));
        var paths = VerbKeys.Append(ClassKey).Append(ApplicationKey).ToArray();
        foreach (var path in paths)
        {
            EnsureOwnedOrMissing(classes, path);
        }

        var belongsToThisBuild = false;
        foreach (var path in paths)
        {
            using var key = classes.OpenSubKey(path);
            var binding = key?.GetValue(BrokerValue, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (binding is not null)
            {
                if (binding is not string broker || !broker.Equals(expectedBroker, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                belongsToThisBuild = true;
            }
        }

        using (var server = classes.OpenSubKey(ClassKey + "\\LocalServer32"))
        {
            var executable = server?.GetValue("ServerExecutable", null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (executable is not null)
            {
                if (executable is not string broker || !broker.Equals(expectedBroker, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                belongsToThisBuild = true;
            }

            var command = server?.GetValue(string.Empty, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (command is not null)
            {
                if (command is not string text || !text.Equals(CommandEncoding.WindowsArgument(expectedBroker), StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                belongsToThisBuild = true;
            }
        }

        // New registrations carry the build identity on every owned key, so
        // partial cleanup does not depend on a surviving CLSID/server key.
        // Legacy owner-only fragments cannot identify a build: leave them alone.
        if (belongsToThisBuild)
        {
            foreach (var path in paths)
            {
                classes.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
            }
        }
    }

    public static bool IsRegistered(RegistryKey classes, string broker)
    {
        ArgumentNullException.ThrowIfNull(classes);
        using var registration = new RegistrationLock(GetRegistrationMutexName(classes));
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

    private sealed class RegistrationLock : IDisposable
    {
        private readonly Mutex mutex;
        private bool acquired;

        public RegistrationLock(string name)
        {
            mutex = new Mutex(initiallyOwned: false, name);
            try
            {
                try
                {
                    acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
                }
                catch (AbandonedMutexException)
                {
                    // WaitOne grants ownership when the previous holder died.
                    // Validate every registry owner/binding again under this lock.
                    acquired = true;
                }

                if (!acquired)
                {
                    throw new TimeoutException("Another Try Run registration operation is still running. No Explorer registration was changed; try again after it finishes.");
                }
            }
            catch
            {
                mutex.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                if (acquired)
                {
                    acquired = false;
                    mutex.ReleaseMutex();
                }
            }
            finally
            {
                mutex.Dispose();
            }
        }
    }
}
