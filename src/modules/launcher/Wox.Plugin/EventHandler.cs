// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Input;

namespace Wox.Plugin
{
    public delegate void WoxKeyDownEventHandler(WoxKeyDownEventArgs e);

    public delegate void AfterWoxQueryEventHandler(WoxQueryEventArgs e);

    public delegate void ResultItemDropEventHandler(Result result, IDataObject dropObject, DragEventArgs e);

    public class WoxKeyDownEventArgs
    {
        public string Query { get; set; }

        public KeyEventArgs keyEventArgs { get; set; }
    }

    public class WoxQueryEventArgs
    {
        public Query Query { get; set; }
    }
}
