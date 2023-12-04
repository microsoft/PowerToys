using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernWpf.Controls
{
    public sealed class ContentDialogClosingDeferral
    {
        private readonly Action _handler;

        internal ContentDialogClosingDeferral(Action handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Complete()
        {
            _handler();
        }
    }
}
