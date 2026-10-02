// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using static Peek.Common.Helpers.AudioSessionInterfaces;

namespace Peek.Common.Helpers
{
    /// <summary>
    /// Restores and saves the current process's Windows volume mixer level.
    /// MediaPlayer.Volume is a separate, per-player gain and does not report mixer changes.
    /// </summary>
    [GeneratedComClass]
    public sealed partial class AudioSessionVolumeHelper : IDisposable, ISessionNotification, ISessionEvents
    {
        private static readonly StrategyBasedComWrappers Wrappers = new();
        private readonly Lock _lock = new();
        private readonly Guid _eventContext = Guid.NewGuid();
        private readonly Func<double> _getVolume;
        private readonly Action<double> _saveVolume;
        private readonly Action<Exception> _reportError;
        private readonly List<ISessionControl> _sessions = [];
        private readonly Channel<(nint Session, float Volume)> _notifications = Channel.CreateUnbounded<(nint Session, float Volume)>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false,
        });

        private readonly Task _notificationTask;
        private ISessionManager? _manager;
        private nint _mtaCookie;
        private bool _disposed;

        private AudioSessionVolumeHelper(Func<double> getVolume, Action<double> saveVolume, Action<Exception> reportError)
        {
            _getVolume = getVolume;
            _saveVolume = saveVolume;
            _reportError = reportError;
            _notificationTask = ProcessNotificationsAsync();
        }

        public static async Task<AudioSessionVolumeHelper?> CreateAsync(Func<double> getVolume, Action<double> saveVolume, Action<Exception> reportError)
        {
            ArgumentNullException.ThrowIfNull(getVolume);
            ArgumentNullException.ThrowIfNull(saveVolume);
            ArgumentNullException.ThrowIfNull(reportError);

            var helper = new AudioSessionVolumeHelper(getVolume, saveVolume, reportError);
            try
            {
                // Session creation notifications require an initialized MTA, not the UI STA.
                await Task.Run(helper.Initialize).ConfigureAwait(false);
                return helper;
            }
            catch (COMException ex)
            {
                helper.Dispose();
                reportError(ex);
                return null;
            }
        }

        private void Initialize()
        {
            Marshal.ThrowExceptionForHR(CoInitializeEx(0, 0));
            try
            {
                // Keep the MTA alive after this thread-pool work item completes.
                Marshal.ThrowExceptionForHR(CoIncrementMTAUsage(out _mtaCookie));
                Marshal.ThrowExceptionForHR(CoCreateInstance(DeviceEnumeratorClassId, 0, 23, typeof(IDeviceEnumerator).GUID, out var pointer));
                var enumerator = Wrap<IDeviceEnumerator>(pointer);
                try
                {
                    enumerator.GetDefaultAudioEndpoint(0, 0, out pointer);
                    var device = Wrap<IDevice>(pointer);
                    try
                    {
                        device.Activate(typeof(ISessionManager).GUID, 23, 0, out pointer);
                        _manager = Wrap<ISessionManager>(pointer);
                    }
                    finally
                    {
                        Release(device);
                    }
                }
                finally
                {
                    Release(enumerator);
                }

                _manager.RegisterSessionNotification(this);
                _manager.GetSessionEnumerator(out pointer);
                var sessions = Wrap<ISessionEnumerator>(pointer);
                try
                {
                    // GetCount also enables notifications for subsequently created sessions.
                    sessions.GetCount(out var count);
                    for (var i = 0; i < count; i++)
                    {
                        sessions.GetSession(i, out pointer);
                        AttachSession(pointer);
                    }
                }
                finally
                {
                    Release(sessions);
                }
            }
            finally
            {
                CoUninitialize();
            }
        }

        private static T Wrap<T>(nint pointer)
        {
            try
            {
                return (T)Wrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
            }
            finally
            {
                Marshal.Release(pointer);
            }
        }

        private static void Release(object instance)
        {
            ((ComObject)instance).FinalRelease();
        }

        private void AttachSession(nint pointer)
        {
            var session = Wrap<ISessionControl>(pointer);
            var retained = false;
            try
            {
                session.GetProcessId(out var processId);
                if (processId != Environment.ProcessId)
                {
                    return;
                }

                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    // Restore before subscribing, so startup cannot overwrite the saved value.
                    ((ISimpleVolume)session).SetMasterVolume((float)_getVolume(), _eventContext);
                    session.RegisterAudioSessionNotification(this);
                    _sessions.Add(session);
                    retained = true;
                }
            }
            finally
            {
                if (!retained)
                {
                    Release(session);
                }
            }
        }

        void ISessionNotification.OnSessionCreated(nint session)
        {
            // Keep the borrowed pointer alive until the worker consumes the notification.
            Marshal.AddRef(session);
            if (!_notifications.Writer.TryWrite((session, 0)))
            {
                Marshal.Release(session);
            }
        }

        void ISessionEvents.OnSimpleVolumeChanged(float volume, bool muted, nint context)
        {
            if (context != 0 && Marshal.PtrToStructure<Guid>(context) == _eventContext)
            {
                return;
            }

            _notifications.Writer.TryWrite((0, volume));
        }

        private async Task ProcessNotificationsAsync()
        {
            await foreach (var notification in _notifications.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (notification.Session != 0)
                    {
                        AttachSession(notification.Session);
                    }
                    else
                    {
                        SaveVolume(notification.Volume);
                    }
                }
                catch (Exception ex)
                {
                    _reportError(ex);
                }
            }
        }

        private void SaveVolume(float volume)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _saveVolume(volume);
                foreach (var session in _sessions)
                {
                    try
                    {
                        // Audio and video players can use different sessions in the same process.
                        ((ISimpleVolume)session).SetMasterVolume(volume, _eventContext);
                    }
                    catch (COMException ex)
                    {
                        _reportError(ex);
                    }
                }
            }
        }

        void ISessionEvents.OnDisplayNameChanged(nint name, nint context)
        {
        }

        void ISessionEvents.OnIconPathChanged(nint path, nint context)
        {
        }

        void ISessionEvents.OnChannelVolumeChanged(uint count, nint volumes, uint channel, nint context)
        {
        }

        void ISessionEvents.OnGroupingParamChanged(nint grouping, nint context)
        {
        }

        void ISessionEvents.OnStateChanged(int state)
        {
        }

        void ISessionEvents.OnSessionDisconnected(int reason)
        {
        }

        public void Dispose()
        {
            ISessionManager? manager;
            ISessionControl[] sessions;
            nint cookie;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                manager = _manager;
                _manager = null;
                sessions = _sessions.ToArray();
                _sessions.Clear();
                cookie = _mtaCookie;
                _mtaCookie = 0;
            }

            _notifications.Writer.TryComplete();

            // Do not hold the callback lock while asking native code to unregister it.
            try
            {
                if (manager != null)
                {
                    try
                    {
                        manager.UnregisterSessionNotification(this);
                    }
                    catch (COMException ex)
                    {
                        _reportError(ex);
                    }
                }

                foreach (var session in sessions)
                {
                    try
                    {
                        session.UnregisterAudioSessionNotification(this);
                    }
                    catch (COMException ex)
                    {
                        _reportError(ex);
                    }
                    finally
                    {
                        Release(session);
                    }
                }
            }
            finally
            {
                // Drain queued session references while the MTA is still available.
                _notificationTask.GetAwaiter().GetResult();

                if (manager != null)
                {
                    Release(manager);
                }

                if (cookie != 0)
                {
                    var result = CoDecrementMTAUsage(cookie);
                    if (result < 0)
                    {
                        _reportError(Marshal.GetExceptionForHR(result)!);
                    }
                }

                GC.SuppressFinalize(this);
            }
        }
    }
}
