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
/// so a change made in PowerToys Settings or the snippet editor shows the next time the page opens.
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
        if (ReadText(_paths.GeneralSettingsFile) is { } general && TextExpanderModuleState.ReadEnabledFlag(general) is bool enabled)
        {
            _enabledInSettings = enabled;
        }

        if (ReadText(_paths.ModuleSettingsFile) is { } module && TextExpanderSettings.Parse(module) is { } settings)
        {
            _settings = settings;
        }

        var availability = TextExpanderModuleState.Evaluate(TextExpanderPolicy.Evaluate(_policy), _enabledInSettings);
        if (availability != TextExpanderAvailability.On)
        {
            return new SnippetSourceState(availability, _settings, SnippetLibrarySnapshot.Unavailable);
        }

        var (filePath, isDefault) = ResolveSnippetsFile(_settings.SnippetsPath, _paths.ModuleFolder);
        return new SnippetSourceState(availability, _settings, _library.Load(filePath, isDefault, _settings.CaseSensitive));
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
