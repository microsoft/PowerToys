// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace KeyboardManagerEditorUI.Helpers
{
    internal sealed class KeyboardRecordingState
    {
        private readonly Dictionary<ulong, (int Generation, VirtualKey Key)> _pendingRestorations = new();
        private uint _nextRestorationMarker = 0x4B4D0000;

        public int Generation { get; private set; }

        public bool IsRecording { get; private set; }

        public bool ExitRequested { get; private set; }

        public bool HasPendingRestorations => _pendingRestorations.Count != 0;

        public bool NeedsHook => IsRecording || HasPendingRestorations;

        public int StartRecording()
        {
            Generation++;
            IsRecording = true;
            ExitRequested = false;
            return Generation;
        }

        public void StopRecording()
        {
            IsRecording = false;
            ExitRequested = false;
        }

        public void RequestExit()
        {
            ExitRequested = IsRecording;
        }

        public uint AddRestoration(VirtualKey key)
        {
            uint marker = unchecked(++_nextRestorationMarker);
            _pendingRestorations.Add(marker, (Generation, key));
            return marker;
        }

        public void RemoveUnsentRestoration(ulong marker)
        {
            _pendingRestorations.Remove(marker);
        }

        public bool TryCompleteRestoration(ulong marker, IReadOnlySet<VirtualKey> heldKeys, out bool suppress)
        {
            suppress = false;
            if (!_pendingRestorations.Remove(marker, out var restoration))
            {
                return false;
            }

            // A canceled generation must never inject Shift into a new recording. A release
            // that reached the hook before this restoration also makes the down obsolete.
            suppress = !IsRecording || !ExitRequested || restoration.Generation != Generation || !heldKeys.Contains(restoration.Key);
            return true;
        }

        public bool CanCompleteExit(int generation)
        {
            // Older canceled input keeps the hook alive, but must not delay the current exit.
            return IsRecording && ExitRequested && generation == Generation &&
                !_pendingRestorations.Values.Any(restoration => restoration.Generation == generation);
        }
    }
}
