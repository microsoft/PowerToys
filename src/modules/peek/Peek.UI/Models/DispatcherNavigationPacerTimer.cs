// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;

namespace Peek.UI.Models
{
    /// <summary>
    /// Adapts a WinUI dispatcher timer for navigation pacing, keeping navigation
    /// callbacks on the UI thread while the pacing component remains independently
    /// testable.
    /// </summary>
    internal sealed class DispatcherNavigationPacerTimer : INavigationPacerTimer
    {
        private readonly DispatcherTimer _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DispatcherNavigationPacerTimer"/>
        /// class on the UI thread that will own navigation pacing.
        /// </summary>
        /// <param name="interval">The interval between pacing ticks.</param>
        public DispatcherNavigationPacerTimer(TimeSpan interval)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _timer.Tick += (_, _) => Tick?.Invoke();
        }

        /// <inheritdoc/>
        public event Action? Tick;

        /// <inheritdoc/>
        public bool IsEnabled => _timer.IsEnabled;

        /// <inheritdoc/>
        public void Start() => _timer.Start();

        /// <inheritdoc/>
        public void Stop() => _timer.Stop();
    }
}
