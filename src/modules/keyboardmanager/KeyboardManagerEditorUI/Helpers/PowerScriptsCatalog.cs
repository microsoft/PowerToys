// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>Discovers ordinary no-input scripts through the shared PowerScripts consumer contract.</summary>
    public static class PowerScriptsCatalog
    {
        private const string HostExeName = "PowerScripts.Host.exe";
        private static IReadOnlyList<PowerScriptInfo> _latestScripts = Array.Empty<PowerScriptInfo>();

        public static string? ResolveHostPath()
        {
            string? overridePath = Environment.GetEnvironmentVariable(PowerScriptsProtocol.HostPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            {
                return overridePath;
            }

            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            if (string.Equals(directory.Name, "WinUI3Apps", StringComparison.OrdinalIgnoreCase) && directory.Parent is not null)
            {
                string sibling = Path.Combine(directory.Parent.FullName, "PowerScripts", HostExeName);
                if (File.Exists(sibling))
                {
                    return sibling;
                }
            }

            return PowerScriptsClient.ResolveHostPath();
        }

        public static async Task<PowerScriptsCatalogSnapshot> GetSystemScriptsAsync(CancellationToken cancellationToken = default)
        {
            string? hostPath = null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                hostPath = await Task.Run(ResolveHostPath, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                if (hostPath is null)
                {
                    return new(null, Array.Empty<PowerScriptInfo>(), ResourceHelper.GetString("PowerScript_HostUnavailable"));
                }

                var client = new PowerScriptsClient(PowerScriptConsumer.KeyboardManager, hostPath);
                var scripts = await client.ListAsync(timeout.Token).ConfigureAwait(false);
                IReadOnlyList<PowerScriptInfo> actions = scripts.Where(script => script.IsAction)
                    .Select(script => new PowerScriptInfo(script)).ToList().AsReadOnly();
                Volatile.Write(ref _latestScripts, actions);
                return new(hostPath, actions, null);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new(hostPath, Array.Empty<PowerScriptInfo>(), ResourceHelper.GetString("PowerScript_DiscoveryTimeout"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new(hostPath, Array.Empty<PowerScriptInfo>(), ex.Message);
            }
        }

        public static bool IsPowerScriptProgramPath(string? programPath) =>
            !string.IsNullOrEmpty(programPath) &&
            string.Equals(Path.GetFileName(programPath), HostExeName, StringComparison.OrdinalIgnoreCase);

        public static string? ParseScriptId(string? arguments) =>
            PowerScriptInvocation.TryParseArguments(arguments, out string id, out _) ? id : null;

        /// <summary>Uses the last successful snapshot, avoiding a Host launch for each saved row.</summary>
        public static string GetScriptName(string id) =>
            Volatile.Read(ref _latestScripts).FirstOrDefault(script => string.Equals(script.Id, id, StringComparison.Ordinal))?.Name ?? id;
    }
}
