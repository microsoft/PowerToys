using System.Threading;
using System.Threading.Tasks;

namespace PowerToys.ModuleContracts;

/// <summary>
/// Base contract for PowerToys modules exposed to the Command Palette.
/// </summary>
public interface IModuleService
{
    /// <summary>
    /// Module identifier (e.g., Workspaces, Awake).
    /// </summary>
    string Key { get; }

    Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> OpenSettingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Workspaces-specific operations.
/// </summary>
public interface IWorkspaceService : IModuleService
{
    Task<OperationResult> LaunchWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<OperationResult> LaunchEditorAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> SnapshotAsync(string? targetPath = null, CancellationToken cancellationToken = default);
}
