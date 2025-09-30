// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Logging;

namespace TopToolbar.Services
{
    /// <summary>
    /// Client for communicating with the WorkspacesService running in PowerToys Runner.
    /// Uses IPC (named pipes) to send workspace launch requests.
    /// </summary>
    public class WorkspacesServiceClient : IDisposable
    {
        private const string PipeName = "powertoys_workspaces_service_";
        private const int TimeoutMs = 5000; // 5 seconds timeout
        private bool _disposed;

        /// <summary>
        /// Launches a workspace by sending an IPC message to the WorkspacesService.
        /// </summary>
        /// <param name="workspaceId">The ID of the workspace to launch</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>True if the workspace was launched successfully</returns>
        public async Task<bool> LaunchWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                throw new ArgumentException("Workspace ID cannot be null or empty", nameof(workspaceId));
            }

            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);

                // Connect to the named pipe with timeout
                await pipe.ConnectAsync(TimeoutMs, cancellationToken).ConfigureAwait(false);

                if (!pipe.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Failed to connect to pipe '{PipeName}' for workspace '{workspaceId}'");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Successfully connected to pipe '{PipeName}' for workspace '{workspaceId}'");

                // Send the workspace ID as UTF-16 bytes (compatible with std::wstring)
                var messageBytes = Encoding.Unicode.GetBytes(workspaceId);
                await pipe.WriteAsync(messageBytes, cancellationToken).ConfigureAwait(false);
                await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Successfully sent workspace launch request for '{workspaceId}'");
                return true;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Timeout launching workspace '{workspaceId}' after {TimeoutMs}ms: {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: IO error launching workspace '{workspaceId}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Unexpected error launching workspace '{workspaceId}': {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the WorkspacesService is running and accessible.
        /// </summary>
        /// <returns>True if the service is available</returns>
        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                using var cts = new CancellationTokenSource(1000); // Short timeout for availability check

                await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
                return pipe.IsConnected;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Timeout connecting to pipe '{PipeName}': {ex.Message}");
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: IO error connecting to pipe '{PipeName}': {ex.Message}");
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Access denied to pipe '{PipeName}': {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WorkspacesServiceClient: Unexpected error connecting to pipe '{PipeName}': {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose pattern implementation.
        /// </summary>
        /// <param name="disposing">True when called from Dispose to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here, if any.
                }

                // Free unmanaged resources here, if any.
                _disposed = true;
            }
        }
    }
}
