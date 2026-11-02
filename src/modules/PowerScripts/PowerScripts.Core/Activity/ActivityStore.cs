// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerScripts.Core.Storage;

namespace PowerScripts.Core.Activity;

/// <summary>A bounded, per-user operational history; not a security audit or trust store.</summary>
public sealed class ActivityStore
{
    public const int MaximumRecords = 200;
    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly string _directory;
    private readonly string _path;
    private readonly string _mutexName;

    public ActivityStore(string? directory = null)
    {
        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory ??
            (SettingsStore.Current as FileSettingsStore)?.BaseDirectory ??
            PowerScriptsPaths.ModuleDirectory));
        _path = Path.Combine(_directory, "activity.json");
        var identity = OperatingSystem.IsWindows() ? _directory.ToUpperInvariant() : _directory;
        _mutexName = "Global\\PowerToys.PowerScripts.Activity." +
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    public ActivityRecord Start(string scriptId, string scriptName, string consumerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptName);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerId);
        using var process = Process.GetCurrentProcess();
        var record = new ActivityRecord
        {
            Id = Guid.NewGuid(),
            ScriptId = scriptId[..Math.Min(scriptId.Length, 256)],
            ScriptName = scriptName[..Math.Min(scriptName.Length, 256)],
            ConsumerId = consumerId[..Math.Min(consumerId.Length, 64)],
            StartedUtc = DateTimeOffset.UtcNow,
            Status = ActivityStatus.Running,
            HostProcessId = process.Id,
            HostStartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
        };
        Update(record);
        return record;
    }

    public void Complete(ActivityRecord record, int exitCode) =>
        Update(record with
        {
            FinishedUtc = DateTimeOffset.UtcNow,
            Status = exitCode == 0 ? ActivityStatus.Succeeded : ActivityStatus.Failed,
            ExitCode = exitCode,
        }, requireExisting: true);

    public IReadOnlyList<ActivityRecord> List() =>
        WithLock(() => Read().Select(ResolveStatus).OrderByDescending(record => record.StartedUtc).ToArray());

    /// <summary>Forget finished history without losing attribution for in-flight invocations.</summary>
    public void ClearCompleted() => WithLock(() =>
    {
        Write(Read().Select(ResolveStatus)
            .Where(record => record.Status is ActivityStatus.Running or ActivityStatus.Unknown).ToArray());
        return true;
    });

    private void Update(ActivityRecord record, bool requireExisting = false) => WithLock(() =>
    {
        var records = Read();
        // A long run may already have aged out; completing it must not resurrect cleared history.
        if (requireExisting && !records.Any(existing => existing.Id == record.Id))
        {
            return false;
        }

        Write(records.Where(existing => existing.Id != record.Id).Append(record)
            .OrderByDescending(existing => existing.StartedUtc).Take(MaximumRecords).ToArray());
        return true;
    });

    private List<ActivityRecord> Read()
    {
        string json;
        try
        {
            using var file = File.OpenRead(_path);
            if (file.Length > 2 * 1024 * 1024)
            {
                throw new InvalidDataException("PowerScripts Activity history exceeds its size limit.");
            }

            using var reader = new StreamReader(file);
            json = reader.ReadToEnd();
        }
        catch (FileNotFoundException)
        {
            return new();
        }

        var records = JsonSerializer.Deserialize<List<ActivityRecord>>(json, JsonOptions)
            ?? throw new InvalidDataException("PowerScripts Activity history is invalid.");
        if (records.Count > MaximumRecords || records.Any(record => record is null))
        {
            throw new InvalidDataException("PowerScripts Activity history is invalid.");
        }

        return records;
    }

    private void Write(IReadOnlyList<ActivityRecord> records)
    {
        var temporaryPath = Path.Combine(_directory, $"activity.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(records, JsonOptions));
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private T WithLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, _mutexName);
        bool acquired;
        try
        {
            acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (!acquired)
        {
            throw new TimeoutException("PowerScripts Activity history is busy. Try again.");
        }

        try
        {
            Directory.CreateDirectory(_directory);
            return action();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private static ActivityRecord ResolveStatus(ActivityRecord record)
    {
        if (record.Status is not (ActivityStatus.Running or ActivityStatus.Unknown))
        {
            return record;
        }

        try
        {
            using var process = Process.GetProcessById(record.HostProcessId);
            if (!process.HasExited && process.StartTime.ToUniversalTime().Ticks == record.HostStartTimeUtcTicks)
            {
                return record with { Status = ActivityStatus.Running };
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // The owning Host exited; its exact completion time and reason are unknowable.
        }
        catch (Win32Exception)
        {
            return record with { Status = ActivityStatus.Unknown };
        }

        return record with { Status = ActivityStatus.Interrupted };
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<ActivityStatus>(JsonNamingPolicy.CamelCase));
        return options;
    }
}
