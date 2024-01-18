// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace FancyZonesEditor.Controls
{
    public sealed class ContentDialogClosingEventArgs : EventArgs
    {
        private ContentDialogClosingDeferral _deferral;
        private int _deferralCount;

        internal ContentDialogClosingEventArgs(ContentDialogResult result)
        {
            Result = result;
        }

        public bool Cancel { get; set; }

        public ContentDialogResult Result { get; }

        public ContentDialogClosingDeferral GetDeferral()
        {
            _deferralCount++;

            return new ContentDialogClosingDeferral(() =>
            {
                DecrementDeferralCount();
            });
        }

        internal void SetDeferral(ContentDialogClosingDeferral deferral)
        {
            _deferral = deferral;
        }

        internal void DecrementDeferralCount()
        {
            _deferralCount--;
            if (_deferralCount == 0)
            {
                _deferral.Complete();
            }
        }

        internal void IncrementDeferralCount()
        {
            _deferralCount++;
        }
    }
}
