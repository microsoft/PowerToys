// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ColorPicker.Helpers
{
    internal sealed class CoalescedAction
    {
        private readonly Func<Action, bool> _tryEnqueue;
        private readonly Action _action;
        private bool _isPending;

        internal CoalescedAction(Func<Action, bool> tryEnqueue, Action action)
        {
            ArgumentNullException.ThrowIfNull(tryEnqueue);
            ArgumentNullException.ThrowIfNull(action);

            _tryEnqueue = tryEnqueue;
            _action = action;
        }

        internal void Request()
        {
            if (_isPending)
            {
                return;
            }

            _isPending = true;
            if (!_tryEnqueue(Execute))
            {
                _isPending = false;
            }
        }

        private void Execute()
        {
            _isPending = false;
            _action();
        }
    }
}
