// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

using System.IO;

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// One-shot migration hook called by the runner on startup.  Reads the
/// legacy %LocalAppData% file (if any), hands it to the service which
/// atomically writes it into the new protected location and drops a
/// .migrated sentinel.  Subsequent calls short-circuit on the sentinel
/// without round-tripping through the service.
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
        if (File.Exists(SettingsPaths.MigrationSentinel()))
        {
            return Outcome.AlreadyMigrated;
        }

        var legacy = SettingsPaths.LegacyWorkspacesFile();
        if (!File.Exists(legacy))
        {
            // Tell the service "I have no legacy data" so it can drop the
            // sentinel and we never reach this branch again.
            var rc = WorkspacesSvcClient.MigrateFromLegacy("{\"workspaces\":[]}");
            return rc == WorkspacesSvcClient.Result.Ok
                ? Outcome.NothingToMigrate
                : Outcome.SkippedServiceUnavailable;
        }

        string content;
        try
        {
            content = File.ReadAllText(legacy);
        }
        catch
        {
            return Outcome.SkippedLegacyUnreadable;
        }

        var result = WorkspacesSvcClient.MigrateFromLegacy(content);
        return result switch
        {
            WorkspacesSvcClient.Result.Ok => Outcome.Migrated,
            WorkspacesSvcClient.Result.ServiceUnavailable => Outcome.SkippedServiceUnavailable,
            _ => Outcome.SkippedServerRejected,
        };
    }
}
