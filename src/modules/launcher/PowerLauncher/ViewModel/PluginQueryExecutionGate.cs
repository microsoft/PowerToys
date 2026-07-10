// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Wox.Plugin;

namespace PowerLauncher.ViewModel
{
    internal sealed class PluginQueryExecutionGate
    {
        private readonly ConcurrentDictionary<PluginPair, SemaphoreSlim> _gates = new();

        public bool TryEnter(PluginPair plugin, out IDisposable lease)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            var gate = _gates.GetOrAdd(plugin, static _ => new SemaphoreSlim(1, 1));
            if (!gate.Wait(0))
            {
                lease = null;
                return false;
            }

            lease = new GateLease(gate);
            return true;
        }

        public async Task<IDisposable> EnterAsync(PluginPair plugin, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plugin);

            var gate = _gates.GetOrAdd(plugin, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new GateLease(gate);
        }

        private sealed class GateLease : IDisposable
        {
            private SemaphoreSlim _gate;

            public GateLease(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _gate, null)?.Release();
            }
        }
    }
}
