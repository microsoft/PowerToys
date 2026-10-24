// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.Mocks
{
    /// <summary>A folder watch the test fires by hand, so waiting can be tested without timers.</summary>
    internal sealed class FakeFileWatch : IDisposable
    {
        private readonly Action _changed;

        public FakeFileWatch(string folder, IReadOnlyList<string> fileNames, Action changed)
        {
            Folder = folder;
            FileNames = fileNames;
            _changed = changed;
        }

        public string Folder { get; }

        public IReadOnlyList<string> FileNames { get; }

        public bool Disposed { get; private set; }

        /// <summary>Invokes the callback even after disposal, as a late timer tick could.</summary>
        public void Trigger() => _changed();

        public void Dispose() => Disposed = true;
    }
}
