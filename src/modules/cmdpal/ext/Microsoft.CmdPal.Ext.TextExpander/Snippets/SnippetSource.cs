// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Security;
using System.Threading;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Reads, on request, whether Text Expander is on, its settings and its snippets. Each read checks the files again,
/// so a change made in PowerToys Settings or the snippet editor shows the next time the page opens. A file that hasn't
/// changed since it was read isn't read again.
/// </summary>
internal sealed class SnippetSource
{
    /// <summary>How long the main search reuses what was read, since it asks on every keystroke.</summary>
    public static readonly TimeSpan SearchMaxAge = TimeSpan.FromSeconds(1);

    private readonly TextExpanderPaths _paths;
    private readonly IPolicyStore _policy;
    private readonly Func<long> _milliseconds;
    private readonly SnippetLibrary _library = new();
    private readonly Lock _lock = new();

    // Guarded by _lock. A settings file caught mid-save keeps what was read before, as the engine does.
    private SnippetSourceState? _state;
    private long _readAt;
    private bool _enabledInSettings;
    private TextExpanderSettings _settings = TextExpanderSettings.Default;

    // Guarded by _lock: each settings file as it was when last read, so one that hasn't changed isn't read again.
    private FileStamp _generalSettingsRead;
    private FileStamp _moduleSettingsRead;

    public SnippetSource()
        : this(TextExpanderPaths.ForCurrentUser, new RegistryPolicyStore(), static () => Environment.TickCount64)
    {
    }

    internal SnippetSource(TextExpanderPaths paths, IPolicyStore policy, Func<long> milliseconds)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(milliseconds);

        _paths = paths;
        _policy = policy;
        _milliseconds = milliseconds;
    }

    /// <summary>Gets the state, read again unless the last read was less than <paramref name="maxAge"/> ago.</summary>
    public SnippetSourceState GetState(TimeSpan maxAge)
    {
        lock (_lock)
        {
            if (_state is not null && _milliseconds() - _readAt < maxAge.TotalMilliseconds)
            {
                return _state;
            }

            _state = Read();
            _readAt = _milliseconds();
            return _state;
        }
    }

    /// <summary>Gets the state as it is now.</summary>
    public SnippetSourceState GetState() => GetState(TimeSpan.Zero);

    /// <summary>
    /// Selects the snippets file by the rule Text Expander uses (<see cref="SnippetPathResolution"/>): the file the
    /// user chose, or the default file in <paramref name="defaultFolder"/> when none, or no usable one, was chosen.
    /// </summary>
    internal static (string FilePath, bool IsDefault) ResolveSnippetsFile(string? configuredPath, string defaultFolder)
    {
        try
        {
            if (SnippetPathResolution.FromConfiguredPath(configuredPath, Environment.ExpandEnvironmentVariables) is { } chosen)
            {
                return (chosen.FilePath, chosen.IsDefault);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException or SecurityException)
        {
            // As in the engine, a setting that can't be used falls back to the default file.
        }

        var fallback = SnippetPathResolution.Default(defaultFolder);
        return (fallback.FilePath, fallback.IsDefault);
    }

    private SnippetSourceState Read()
    {
        // The snippets page asks on every keystroke. Group policy is checked each time; the files only when they changed.
        if (Changed(_paths.GeneralSettingsFile, ref _generalSettingsRead, out var generalStamp)
            && ReadText(_paths.GeneralSettingsFile) is { } general
            && TextExpanderModuleState.ReadEnabledFlag(general) is bool enabled)
        {
            _enabledInSettings = enabled;
            _generalSettingsRead = generalStamp;
        }

        if (Changed(_paths.ModuleSettingsFile, ref _moduleSettingsRead, out var moduleStamp)
            && ReadText(_paths.ModuleSettingsFile) is { } module
            && TextExpanderSettings.Parse(module) is { } settings)
        {
            _settings = settings;
            _moduleSettingsRead = moduleStamp;
        }

        var availability = TextExpanderModuleState.Evaluate(TextExpanderPolicy.Evaluate(_policy), _enabledInSettings);
        if (availability != TextExpanderAvailability.On)
        {
            return new SnippetSourceState(availability, _settings, SnippetLibrarySnapshot.Unavailable);
        }

        var (filePath, isDefault) = ResolveSnippetsFile(_settings.SnippetsPath, _paths.ModuleFolder);
        return new SnippetSourceState(availability, _settings, _library.Load(filePath, isDefault, _settings.CaseSensitive));
    }

    /// <summary>
    /// Whether a settings file may have changed since <paramref name="lastRead"/>, its stamp when it was last read and
    /// understood. If so, that's cleared until it's read and understood again: a save caught halfway is read again next time.
    /// </summary>
    private static bool Changed(string path, ref FileStamp lastRead, out FileStamp stamp)
    {
        stamp = FileStamp.Of(path);
        if (stamp.Exists && stamp == lastRead)
        {
            return false;
        }

        lastRead = default;
        return true;
    }

    private static string? ReadText(string path)
    {
        try
        {
            // Shared, so PowerToys Settings can save while it's read.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }
}
