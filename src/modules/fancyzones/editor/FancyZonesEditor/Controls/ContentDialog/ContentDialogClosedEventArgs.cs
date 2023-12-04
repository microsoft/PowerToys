using System;

namespace ModernWpf.Controls
{
    public class ContentDialogClosedEventArgs : EventArgs
    {
        internal ContentDialogClosedEventArgs(ContentDialogResult result)
        {
            Result = result;
        }

        public ContentDialogResult Result { get; }
    }
}
