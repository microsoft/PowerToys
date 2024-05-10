// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using HostsUILib.Models;
using HostsUILib.Settings;
using Microsoft.UI.Dispatching;

namespace HostsUILib.Helpers
{
    public class DuplicateService : IDuplicateService, IDisposable
    {
        private record struct Check(string Address, string[] Hosts);

        private readonly IUserSettings _userSettings;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly Queue<Check> _checkQueue;
        private readonly ManualResetEvent _checkEvent;
        private readonly Thread _queueThread;

        private readonly string[] _loopbackAddresses =
        {
            "0.0.0.0",
            "::",
            "::0",
            "0:0:0:0:0:0:0:0",
            "127.0.0.1",
            "::1",
            "0:0:0:0:0:0:0:1",
        };

        private ReadOnlyCollection<Entry> _entries;
        private bool _disposed;

        public DuplicateService(IUserSettings userSettings)
        {
            _userSettings = userSettings;

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _checkQueue = new Queue<Check>();
            _checkEvent = new ManualResetEvent(false);

            _queueThread = new Thread(ProcessQueue);
            _queueThread.IsBackground = true;
            _queueThread.Start();
        }

        public void Initialize(IList<Entry> entries)
        {
            _entries = entries.AsReadOnly();

            if (_checkQueue.Count > 0)
            {
                _checkQueue.Clear();
            }

            foreach (var entry in _entries)
            {
                if (!_userSettings.LoopbackDuplicates && _loopbackAddresses.Contains(entry.Address))
                {
                    continue;
                }

                _checkQueue.Enqueue(new Check(entry.Address, entry.SplittedHosts));
            }

            _checkEvent.Set();
        }

        public void CheckDuplicates(string address, string[] hosts)
        {
            _checkQueue.Enqueue(new Check(address, hosts));
            _checkEvent.Set();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void ProcessQueue()
        {
            while (true)
            {
                _checkEvent.WaitOne();

                while (_checkQueue.Count > 0)
                {
                    var check = _checkQueue.Dequeue();
                    FindDuplicates(check.Address, check.Hosts);
                }

                _checkEvent.Reset();
            }
        }

        private void FindDuplicates(string address, string[] hosts)
        {
            var entries = _entries.Where(e =>
                string.Equals(e.Address, address, StringComparison.OrdinalIgnoreCase)
                || hosts.Intersect(e.SplittedHosts, StringComparer.OrdinalIgnoreCase).Any());

            foreach (var entry in entries)
            {
                SetDuplicate(entry);
            }
        }

        private void SetDuplicate(Entry entry)
        {
            if (!_userSettings.LoopbackDuplicates && _loopbackAddresses.Contains(entry.Address))
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    entry.Duplicate = false;
                });

                return;
            }

            var duplicate = false;

            /*
             * Duplicate are based on the following criteria:
             * Entries with the same type and at least one host in common
             * Entries with the same type and address, except when there is only one entry with less than 9 hosts for that type and address
             */
            if (_entries.Any(e => e != entry
                && e.Type == entry.Type
                && entry.SplittedHosts.Intersect(e.SplittedHosts, StringComparer.OrdinalIgnoreCase).Any()))
            {
                duplicate = true;
            }
            else if (_entries.Any(e => e != entry
                && e.Type == entry.Type
                && string.Equals(e.Address, entry.Address, StringComparison.OrdinalIgnoreCase)))
            {
                duplicate = entry.SplittedHosts.Length < Consts.MaxHostsCount
                    && _entries.Count(e => e.Type == entry.Type
                        && string.Equals(e.Address, entry.Address, StringComparison.OrdinalIgnoreCase)
                        && e.SplittedHosts.Length < Consts.MaxHostsCount) > 1;
            }

            _dispatcherQueue.TryEnqueue(() => entry.Duplicate = duplicate);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _checkEvent?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}
