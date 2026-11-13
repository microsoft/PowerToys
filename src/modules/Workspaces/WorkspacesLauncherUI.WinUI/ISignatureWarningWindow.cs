// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace WorkspacesLauncherUI
{
    internal interface ISignatureWarningWindow
    {
        event EventHandler Rendered;

        event EventHandler<bool> ChoiceMade;

        bool IsVisible { get; }

        void Show();

        void EnableRunChoice();

        void DismissWithoutResponse();
    }
}
