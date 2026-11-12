// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Abstraction over <see cref="AppDomain.ProcessExit"/> so <see cref="CrashDetectionScope"/>
    /// can be unit-tested without invoking real process termination.
    /// </summary>
    public interface IProcessExitHook
    {
        void Subscribe(EventHandler handler);

        void Unsubscribe(EventHandler handler);
    }

    /// <summary>
    /// Default production implementation that bridges to <see cref="AppDomain.ProcessExit"/>.
    /// </summary>
    internal sealed class AppDomainProcessExitHook : IProcessExitHook
    {
        public static readonly AppDomainProcessExitHook Instance = new AppDomainProcessExitHook();

        private AppDomainProcessExitHook()
        {
        }

        public void Subscribe(EventHandler handler) => AppDomain.CurrentDomain.ProcessExit += handler;

        public void Unsubscribe(EventHandler handler) => AppDomain.CurrentDomain.ProcessExit -= handler;
    }
}
