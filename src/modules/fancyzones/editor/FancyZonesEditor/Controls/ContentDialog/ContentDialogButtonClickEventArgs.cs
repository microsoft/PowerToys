using System;
using System.Diagnostics;

namespace ModernWpf.Controls
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
            Debug.Assert(_deferralCount > 0);
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
