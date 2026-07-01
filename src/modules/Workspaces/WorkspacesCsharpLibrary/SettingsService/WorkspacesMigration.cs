// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// One-shot legacy migration, called by the runner on startup (idempotent).
/// The service has no "migrate" concept (Design-v6-Final.md §10): migration is
/// simply "read the legacy %LocalAppData% file once and PutBlob it through the
/// service".  A sentinel under %LocalAppData% short-circuits subsequent calls.
/// </summary>
public static class WorkspacesMigration
{
    public enum Outcome
    {
        AlreadyMigrated,
        NothingToMigrate,
        Migrated,
        SkippedServiceUnavailable,
        SkippedLegacyUnreadable,
        SkippedServerRejected,
    }

    public static Outcome Run()
    {
        var sentinel = SettingsPaths.MigrationSentinel();
        if (File.Exists(sentinel))
        {
            return Outcome.AlreadyMigrated;
        }

        // If the service already holds a blob for this user, another runner
        // invocation migrated it; drop the sentinel and stop.
        var probe = PTSettingsClient.GetBlob(out var existing);
        if (probe == PTSettingsClient.Result.Ok && existing.Length > 0)
        {
            TryWriteSentinel(sentinel);
            return Outcome.AlreadyMigrated;
        }

        if (probe == PTSettingsClient.Result.Unavailable)
        {
            return Outcome.SkippedServiceUnavailable;
        }

        // probe is NotFound (no blob yet) or a transient error — proceed only
        // when we positively know there is nothing yet.
        if (probe != PTSettingsClient.Result.NotFound)
        {
            return Outcome.SkippedServerRejected;
        }

        var legacy = SettingsPaths.LegacyWorkspacesFile();
        if (!File.Exists(legacy))
        {
            TryWriteSentinel(sentinel);
            return Outcome.NothingToMigrate;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(legacy);
        }
        catch (IOException)
        {
            return Outcome.SkippedLegacyUnreadable;
        }
        catch (System.UnauthorizedAccessException)
        {
            return Outcome.SkippedLegacyUnreadable;
        }

        var put = PTSettingsClient.PutBlob(bytes);
        switch (put)
        {
            case PTSettingsClient.Result.Ok:
                // Keep the legacy file as a backup for one release; the service
                // blob is the authority going forward.
                TryWriteSentinel(sentinel);
                return Outcome.Migrated;

            case PTSettingsClient.Result.Unavailable:
                return Outcome.SkippedServiceUnavailable;

            default:
                return Outcome.SkippedServerRejected;
        }
    }

    private static void TryWriteSentinel(string sentinel)
    {
        try
        {
            var dir = Path.GetDirectoryName(sentinel);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(sentinel, System.DateTime.UtcNow.ToString("o"));
        }
        catch (IOException)
        {
            // Best-effort: if we can't write the sentinel we simply re-probe
            // next time, which is cheap and idempotent.
        }
        catch (System.UnauthorizedAccessException)
        {
        }
    }
}
