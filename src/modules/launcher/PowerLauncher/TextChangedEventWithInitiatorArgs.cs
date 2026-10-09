// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PowerLauncher
{
    internal sealed class TextChangedEventWithInitiatorArgs : TextChangedEventArgs
    {
        public TextChangedEventWithInitiatorArgs(RoutedEvent id, UndoAction action)
            : base(id, action)
        {
        }

        public bool IsTextSetProgrammatically { get; set; }
    }
}
