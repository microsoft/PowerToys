// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Waits for an existing engine gesture to finish before capturing a recording session.
    /// </summary>
    internal sealed class EditorCaptureState
    {
        public bool IsReady { get; private set; }

        public bool ShouldCapture(bool sourceEvent, bool keyUp, bool engineReady)
        {
            if (!sourceEvent)
            {
                return false;
            }

            if (!IsReady)
            {
                IsReady = engineReady;

                // A key-up that signals readiness still belongs to the previous gesture.
                if (keyUp)
                {
                    return false;
                }
            }

            return IsReady;
        }

        public void Reset()
        {
            IsReady = false;
        }
    }
}
