// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Watches one file for being rewritten, in place or by a replacement. The engine and the
    /// editor save settings.json by writing a temporary file and renaming it over the old one, and
    /// a rename raises no <see cref="FileSystemWatcher.Changed"/> event for the target: it shows up
    /// only as <see cref="FileSystemWatcher.Renamed"/> (or Deleted + Renamed), and only when file
    /// names are part of the filter. So this watcher listens for all of them.
    /// </summary>
    internal sealed class FileReplacementWatcher : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly Action _onChanged;

        /// <summary>
        /// Starts watching <paramref name="fileName"/> in <paramref name="directory"/>.
        /// <paramref name="onChanged"/> runs on a thread-pool thread, often several times per write.
        /// </summary>
        internal FileReplacementWatcher(string directory, string fileName, Action onChanged)
        {
            _onChanged = onChanged;
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            _watcher.Changed += OnEvent;
            _watcher.Created += OnEvent;
            _watcher.Deleted += OnEvent;
            _watcher.Renamed += OnEvent;

            // The watcher's buffer overflowed and events were dropped: look at the file once.
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnEvent;
            _watcher.Created -= OnEvent;
            _watcher.Deleted -= OnEvent;
            _watcher.Renamed -= OnEvent;
            _watcher.Error -= OnError;
            _watcher.Dispose();
        }

        private void OnEvent(object sender, FileSystemEventArgs e) => _onChanged();

        private void OnError(object sender, ErrorEventArgs e) => _onChanged();
    }
}
