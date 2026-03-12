// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ManagedCommon;

namespace KeyboardManager.ModuleServices;

public sealed class KeyboardManagerMappingService
{
    public static KeyboardManagerMappingService Instance { get; } = new();

    public Task<PowerToys.ModuleContracts.OperationResult<IReadOnlyList<KeyboardManagerMappingRecord>>> GetMappingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var session = new MappingConfigurationSession();
            if (!KeyboardManagerInterop.LoadMappingSettings(session.Handle))
            {
                return Task.FromResult(PowerToys.ModuleContracts.OperationResults.Fail<IReadOnlyList<KeyboardManagerMappingRecord>>("Failed to load Keyboard Manager mappings."));
            }

            var mappings = new List<KeyboardManagerMappingRecord>();
            LoadSingleKeyMappings(session.Handle, mappings);
            LoadSingleKeyTextMappings(session.Handle, mappings);
            LoadShortcutMappings(session.Handle, mappings);

            return Task.FromResult(PowerToys.ModuleContracts.OperationResults.Ok<IReadOnlyList<KeyboardManagerMappingRecord>>(mappings));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(PowerToys.ModuleContracts.OperationResults.Fail<IReadOnlyList<KeyboardManagerMappingRecord>>("Keyboard Manager mapping query was cancelled."));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to enumerate Keyboard Manager mappings: {ex.Message}");
            return Task.FromResult(PowerToys.ModuleContracts.OperationResults.Fail<IReadOnlyList<KeyboardManagerMappingRecord>>($"Failed to enumerate Keyboard Manager mappings: {ex.Message}"));
        }
    }

    private static void LoadSingleKeyMappings(IntPtr handle, ICollection<KeyboardManagerMappingRecord> mappings)
    {
        var count = KeyboardManagerInterop.GetSingleKeyRemapCount(handle);
        for (var i = 0; i < count; i++)
        {
            var native = default(NativeSingleKeyMapping);
            if (!KeyboardManagerInterop.GetSingleKeyRemap(handle, i, ref native))
            {
                continue;
            }

            var originalDisplay = GetKeyDisplayName(native.OriginalKey);
            var targetRaw = KeyboardManagerInterop.GetStringAndFree(native.TargetKey);
            var targetDisplay = native.IsShortcut ? FormatShortcut(targetRaw) : FormatSingleKey(targetRaw);
            var kind = native.IsShortcut ? KeyboardManagerMappingKind.SingleKeyToShortcut : KeyboardManagerMappingKind.SingleKeyToKey;

            mappings.Add(new KeyboardManagerMappingRecord
            {
                Id = CreateId("single-key", native.OriginalKey.ToString(CultureInfo.InvariantCulture), targetRaw, native.IsShortcut.ToString()),
                Kind = kind,
                TriggerDisplay = originalDisplay,
                TargetDisplay = targetDisplay,
                Subtitle = $"Maps to {targetDisplay}",
                OriginalKeys = native.OriginalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = targetRaw,
            });
        }
    }

    private static void LoadSingleKeyTextMappings(IntPtr handle, ICollection<KeyboardManagerMappingRecord> mappings)
    {
        var count = KeyboardManagerInterop.GetSingleKeyToTextRemapCount(handle);
        for (var i = 0; i < count; i++)
        {
            var native = default(NativeKeyboardTextMapping);
            if (!KeyboardManagerInterop.GetSingleKeyToTextRemap(handle, i, ref native))
            {
                continue;
            }

            var originalDisplay = GetKeyDisplayName(native.OriginalKey);
            var targetText = KeyboardManagerInterop.GetStringAndFree(native.TargetText);
            mappings.Add(new KeyboardManagerMappingRecord
            {
                Id = CreateId("single-key-text", native.OriginalKey.ToString(CultureInfo.InvariantCulture), targetText),
                Kind = KeyboardManagerMappingKind.SingleKeyToText,
                TriggerDisplay = originalDisplay,
                TargetDisplay = targetText,
                Subtitle = $"Types {targetText}",
                OriginalKeys = native.OriginalKey.ToString(CultureInfo.InvariantCulture),
                TargetText = targetText,
            });
        }
    }

    private static void LoadShortcutMappings(IntPtr handle, ICollection<KeyboardManagerMappingRecord> mappings)
    {
        var count = KeyboardManagerInterop.GetShortcutRemapCount(handle);
        for (var i = 0; i < count; i++)
        {
            var native = default(NativeShortcutMapping);
            if (!KeyboardManagerInterop.GetShortcutRemap(handle, i, ref native))
            {
                continue;
            }

            var originalKeys = KeyboardManagerInterop.GetStringAndFree(native.OriginalKeys);
            var targetKeys = KeyboardManagerInterop.GetStringAndFree(native.TargetKeys);
            var targetApp = KeyboardManagerInterop.GetStringAndFree(native.TargetApp);
            var targetText = KeyboardManagerInterop.GetStringAndFree(native.TargetText);
            var programPath = KeyboardManagerInterop.GetStringAndFree(native.ProgramPath);
            var programArgs = KeyboardManagerInterop.GetStringAndFree(native.ProgramArgs);
            var startInDirectory = KeyboardManagerInterop.GetStringAndFree(native.StartInDirectory);
            var uriToOpen = KeyboardManagerInterop.GetStringAndFree(native.UriToOpen);

            var triggerDisplay = FormatShortcut(originalKeys);
            var (kind, targetDisplay, subtitle) = DescribeShortcutTarget(native.OperationType, targetKeys, targetText, programPath, uriToOpen);

            mappings.Add(new KeyboardManagerMappingRecord
            {
                Id = CreateId("shortcut", originalKeys, targetKeys, targetText, programPath, programArgs, startInDirectory, uriToOpen, targetApp, native.OperationType.ToString(CultureInfo.InvariantCulture)),
                Kind = kind,
                TriggerDisplay = triggerDisplay,
                TargetDisplay = targetDisplay,
                Subtitle = string.IsNullOrWhiteSpace(targetApp) ? subtitle : $"{subtitle} in {targetApp}",
                IsAppSpecific = !string.IsNullOrWhiteSpace(targetApp),
                TargetApp = targetApp,
                OriginalKeys = originalKeys,
                TargetKeys = targetKeys,
                TargetText = targetText,
                ProgramPath = programPath,
                ProgramArgs = programArgs,
                StartInDirectory = startInDirectory,
                Elevation = native.Elevation,
                IfRunningAction = native.IfRunningAction,
                Visibility = native.Visibility,
                UriToOpen = uriToOpen,
            });
        }
    }

    private static (KeyboardManagerMappingKind Kind, string TargetDisplay, string Subtitle) DescribeShortcutTarget(int operationType, string targetKeys, string targetText, string programPath, string uriToOpen)
    {
        return operationType switch
        {
            1 => (KeyboardManagerMappingKind.ShortcutToProgram, programPath, $"Opens {programPath}"),
            2 => (KeyboardManagerMappingKind.ShortcutToUri, uriToOpen, $"Opens {uriToOpen}"),
            3 => (KeyboardManagerMappingKind.ShortcutToText, targetText, $"Types {targetText}"),
            _ => DescribeShortcutRemap(targetKeys),
        };
    }

    private static (KeyboardManagerMappingKind Kind, string TargetDisplay, string Subtitle) DescribeShortcutRemap(string targetKeys)
    {
        var keyCodes = ParseKeyCodes(targetKeys);
        if (keyCodes.Count <= 1)
        {
            var targetDisplay = FormatSingleKey(targetKeys);
            return (KeyboardManagerMappingKind.ShortcutToKey, targetDisplay, $"Maps to {targetDisplay}");
        }

        var shortcutDisplay = FormatShortcut(targetKeys);
        return (KeyboardManagerMappingKind.ShortcutToShortcut, shortcutDisplay, $"Maps to {shortcutDisplay}");
    }

    private static string FormatSingleKey(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyCode))
        {
            return GetKeyDisplayName(keyCode);
        }

        return value;
    }

    private static string FormatShortcut(string value)
    {
        var parts = ParseKeyCodes(value)
            .Select(GetKeyDisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        return parts.Length == 0 ? value : string.Join(" + ", parts);
    }

    private static IReadOnlyList<int> ParseKeyCodes(string value)
    {
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var keyCode) ? keyCode : (int?)null)
            .Where(keyCode => keyCode.HasValue)
            .Select(keyCode => keyCode!.Value)
            .ToArray();
    }

    private static string GetKeyDisplayName(int keyCode)
    {
        var buffer = new StringBuilder(64);
        KeyboardManagerInterop.GetKeyDisplayName(keyCode, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string CreateId(params string[] values)
    {
        var payload = string.Join("|", values);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash[..8]);
    }

    private sealed partial class MappingConfigurationSession : IDisposable
    {
        public MappingConfigurationSession()
        {
            Handle = KeyboardManagerInterop.CreateMappingConfiguration();
            if (Handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create Keyboard Manager mapping configuration.");
            }
        }

        public IntPtr Handle { get; private set; }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                KeyboardManagerInterop.DestroyMappingConfiguration(Handle);
                Handle = IntPtr.Zero;
            }
        }
    }
}
