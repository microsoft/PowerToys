// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace ViewModelTests
{
    internal sealed class LifetimeTestRepository<T> : ISettingsRepository<T>
    {
        private Action<T> _settingsChanged;

        internal LifetimeTestRepository(T settings)
        {
            SettingsConfig = settings;
        }

        public event Action<T> SettingsChanged
        {
            add => _settingsChanged += value;
            remove
            {
                _settingsChanged -= value;
                RemovedCount++;
            }
        }

        public T SettingsConfig { get; set; }

        internal int SubscriberCount => _settingsChanged?.GetInvocationList().Length ?? 0;

        internal int RemovedCount { get; private set; }

        public bool ReloadSettings() => true;

        internal void Notify() => _settingsChanged?.Invoke(SettingsConfig);

        internal Action CaptureNotification()
        {
            var handlers = _settingsChanged;
            var settings = SettingsConfig;
            return () => handlers?.Invoke(settings);
        }
    }
}
