// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Peek.Common.Helpers
{
    internal static partial class AudioSessionInterfaces
    {
        internal static readonly Guid DeviceEnumeratorClassId = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

        [LibraryImport("ole32.dll")]
        internal static partial int CoInitializeEx(nint reserved, uint flags);

        [LibraryImport("ole32.dll")]
        internal static partial void CoUninitialize();

        [LibraryImport("ole32.dll")]
        internal static partial int CoIncrementMTAUsage(out nint cookie);

        [LibraryImport("ole32.dll")]
        internal static partial int CoDecrementMTAUsage(nint cookie);

        [LibraryImport("ole32.dll")]
        internal static partial int CoCreateInstance(in Guid classId, nint outer, uint context, in Guid interfaceId, out nint instance);

        // Only the vtable entries used below and the preceding entries are needed.
        [GeneratedComInterface]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        internal partial interface IDeviceEnumerator
        {
            void EnumAudioEndpoints(int flow, uint state, out nint devices);

            void GetDefaultAudioEndpoint(int flow, int role, out nint device);
        }

        [GeneratedComInterface]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        internal partial interface IDevice
        {
            void Activate(in Guid interfaceId, uint context, nint parameters, out nint instance);
        }

        [GeneratedComInterface]
        [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
        internal partial interface ISessionManager
        {
            void GetAudioSessionControl(in Guid sessionId, uint flags, out nint session);

            void GetSimpleAudioVolume(in Guid sessionId, uint flags, out nint volume);

            void GetSessionEnumerator(out nint sessions);

            void RegisterSessionNotification(ISessionNotification notification);

            void UnregisterSessionNotification(ISessionNotification notification);
        }

        [GeneratedComInterface]
        [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
        internal partial interface ISessionEnumerator
        {
            void GetCount(out int count);

            void GetSession(int index, out nint session);
        }

        [GeneratedComInterface]
        [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
        internal partial interface ISessionControl
        {
            void GetState(out int state);

            void GetDisplayName(out nint name);

            void SetDisplayName(nint name, nint context);

            void GetIconPath(out nint path);

            void SetIconPath(nint path, nint context);

            void GetGroupingParam(out Guid grouping);

            void SetGroupingParam(in Guid grouping, nint context);

            void RegisterAudioSessionNotification(ISessionEvents events);

            void UnregisterAudioSessionNotification(ISessionEvents events);

            void GetSessionIdentifier(out nint identifier);

            void GetSessionInstanceIdentifier(out nint identifier);

            void GetProcessId(out uint processId);
        }

        [GeneratedComInterface]
        [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
        internal partial interface ISimpleVolume
        {
            void SetMasterVolume(float volume, in Guid context);

            void GetMasterVolume(out float volume);
        }

        [GeneratedComInterface]
        [Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08")]
        internal partial interface ISessionNotification
        {
            void OnSessionCreated(nint session);
        }

        [GeneratedComInterface]
        [Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8")]
        internal partial interface ISessionEvents
        {
            void OnDisplayNameChanged(nint name, nint context);

            void OnIconPathChanged(nint path, nint context);

            void OnSimpleVolumeChanged(float volume, [MarshalAs(UnmanagedType.Bool)] bool muted, nint context);

            void OnChannelVolumeChanged(uint count, nint volumes, uint channel, nint context);

            void OnGroupingParamChanged(nint grouping, nint context);

            void OnStateChanged(int state);

            void OnSessionDisconnected(int reason);
        }
    }
}
