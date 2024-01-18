// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace FancyZonesEditor.Controls
{
    public class ContentDialogButtonClickEventArgs : EventArgs
    {
        private ContentDialogButtonClickDeferral _deferral;
        private int _deferralCount;

        internal ContentDialogButtonClickEventArgs()
        {
        }

        public bool Cancel { get; set; }

        public ContentDialogButtonClickDeferral GetDeferral()
        {
            _deferralCount++;

            return new ContentDialogButtonClickDeferral(() =>
            {
                DecrementDeferralCount();
            });
        }

        internal void SetDeferral(ContentDialogButtonClickDeferral deferral)
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
