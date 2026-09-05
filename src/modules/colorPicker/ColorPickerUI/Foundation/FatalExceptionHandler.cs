// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

namespace ColorPicker.Foundation
{
    internal sealed class FatalExceptionHandler
    {
        private readonly Action<Exception> _logException;
        private readonly Action _restoreCursors;
        private int _handled;

        internal FatalExceptionHandler(Action<Exception> logException, Action restoreCursors)
        {
            _logException = logException ?? throw new ArgumentNullException(nameof(logException));
            _restoreCursors = restoreCursors ?? throw new ArgumentNullException(nameof(restoreCursors));
        }

        internal void Handle(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            if (Interlocked.Exchange(ref _handled, 1) != 0)
            {
                return;
            }

            _logException(exception);
            _restoreCursors();
        }
    }
}
