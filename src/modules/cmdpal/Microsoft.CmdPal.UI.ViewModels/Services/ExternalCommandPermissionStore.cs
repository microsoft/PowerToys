// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public sealed partial class ExternalCommandPermissionStore : IExternalCommandPermissionStore, IDisposable
{
    private const string PermissionsFileName = "external-command-permissions.dat";
    private const int MaximumProtectedFileSize = 1024 * 1024;
    private const int MaximumPermissionCount = 1024;

    private readonly IAtRestDataProtector _dataProtector;
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ExternalCommandPermissionState? _state;
    private volatile bool _disposed;

    public event EventHandler? PermissionsChanged;

    public ExternalCommandPermissionStore(IAtRestDataProtector dataProtector, IApplicationInfoService applicationInfoService)
        : this(dataProtector, Path.Combine(applicationInfoService.ConfigDirectory, PermissionsFileName))
    {
    }

    internal ExternalCommandPermissionStore(IAtRestDataProtector dataProtector, string filePath)
    {
        _dataProtector = dataProtector;
        _filePath = filePath;
    }

    public async Task<bool> IsAllowedAsync(ExternalCommandPermissionKey key, CancellationToken cancellationToken = default)
    {
        await EnterGateAsync(cancellationToken);
        try
        {
            var state = await GetStateUnsafeAsync(cancellationToken);
            return state.Permissions.Any(permission => permission.Key == key);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ExternalCommandPermission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnterGateAsync(cancellationToken);
        try
        {
            var state = await GetStateUnsafeAsync(cancellationToken);
            return state.Permissions
                .OrderBy(static permission => permission.ProviderName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static permission => permission.CommandName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RememberAsync(ExternalCommandPermission permission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var changed = false;
        await EnterGateAsync(cancellationToken);
        try
        {
            var state = await GetStateUnsafeAsync(cancellationToken);
            var permissions = state.Permissions.Where(existing => existing.Key != permission.Key).ToList();
            permissions.Add(permission);
            if (permissions.Count > MaximumPermissionCount)
            {
                Logger.LogWarning("The maximum number of external command permissions has been reached.");
                return false;
            }

            changed = await SaveStateUnsafeAsync(new ExternalCommandPermissionState { Permissions = permissions }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
        {
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public async Task<bool> RevokeAsync(ExternalCommandPermissionKey key, CancellationToken cancellationToken = default)
    {
        var changed = false;
        await EnterGateAsync(cancellationToken);
        try
        {
            var state = await GetStateUnsafeAsync(cancellationToken);
            var permissions = state.Permissions.Where(permission => permission.Key != key).ToList();
            if (permissions.Count == state.Permissions.Count)
            {
                return false;
            }

            changed = await SaveStateUnsafeAsync(new ExternalCommandPermissionState { Permissions = permissions }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
        {
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public async Task<bool> ClearAsync(CancellationToken cancellationToken = default)
    {
        var changed = false;
        await EnterGateAsync(cancellationToken);
        try
        {
            var state = await GetStateUnsafeAsync(cancellationToken);
            if (state.Permissions.Count == 0)
            {
                return false;
            }

            changed = await SaveStateUnsafeAsync(new ExternalCommandPermissionState(), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        if (changed)
        {
            PermissionsChanged?.Invoke(this, EventArgs.Empty);
        }

        return changed;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private async Task EnterGateAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken);

        // Recheck because Dispose does not wait for queued operations.
        if (_disposed)
        {
            _gate.Release();
            ThrowIfDisposed();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private async Task<ExternalCommandPermissionState> GetStateUnsafeAsync(CancellationToken cancellationToken)
    {
        if (_state is not null)
        {
            return _state;
        }

        if (!File.Exists(_filePath))
        {
            _state = new();
            return _state;
        }

        try
        {
            var fileInfo = new FileInfo(_filePath);
            if (fileInfo.Length > MaximumProtectedFileSize)
            {
                throw new InvalidDataException("The external command permission file is too large.");
            }

            var protectedData = await File.ReadAllBytesAsync(_filePath, cancellationToken);
            var plaintext = await _dataProtector.UnprotectAsync(protectedData, cancellationToken);
            if (plaintext.Length > MaximumProtectedFileSize)
            {
                throw new InvalidDataException("The unprotected external command permission data is too large.");
            }

            var state = JsonSerializer.Deserialize(plaintext, ExternalCommandPermissionJsonContext.Default.ExternalCommandPermissionState) ?? new();
            ValidateState(state);
            _state = state;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError("Failed to load external command permissions. No remembered permissions will be used.", ex);
            _state = new();
        }

        return _state;
    }

    private async Task<bool> SaveStateUnsafeAsync(ExternalCommandPermissionState state, CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        try
        {
            ValidateState(state);

            var plaintext = JsonSerializer.SerializeToUtf8Bytes(state, ExternalCommandPermissionJsonContext.Default.ExternalCommandPermissionState);
            if (plaintext.Length > MaximumProtectedFileSize)
            {
                throw new InvalidDataException("The external command permission data is too large.");
            }

            var protectedData = await _dataProtector.ProtectAsync(plaintext, cancellationToken);

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(temporaryPath, protectedData, cancellationToken);
            File.Move(temporaryPath, _filePath, overwrite: true);
            temporaryPath = null;
            _state = state;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError("Failed to save external command permissions.", ex);
            return false;
        }
        finally
        {
            if (temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }
    }

    private static void ValidateState(ExternalCommandPermissionState state)
    {
        if (state.Permissions is not { } permissions)
        {
            throw new InvalidDataException("The external command permission file does not contain a permission collection.");
        }

        if (permissions.Count > MaximumPermissionCount)
        {
            throw new InvalidDataException("The external command permission file contains too many entries.");
        }

        foreach (var permission in permissions)
        {
            if (!IsValidPermission(permission))
            {
                throw new InvalidDataException("The external command permission file contains an invalid entry.");
            }
        }
    }

    private static bool IsValidPermission(ExternalCommandPermission? permission)
    {
        if (permission is null ||
            permission.Key is not { } key ||
            permission.CommandName is null ||
            permission.ProviderName is null ||
            key.PackageFamilyName is null ||
            key.ProviderId is null ||
            key.CommandId is null)
        {
            return false;
        }

        return key.Kind switch
        {
            ExternalCommandKind.Command =>
                !string.IsNullOrWhiteSpace(key.ProviderId) &&
                !string.IsNullOrWhiteSpace(key.CommandId),
            ExternalCommandKind.Reload => key == ExternalCommandPermissionKey.Reload,
            _ => false,
        };
    }
}
