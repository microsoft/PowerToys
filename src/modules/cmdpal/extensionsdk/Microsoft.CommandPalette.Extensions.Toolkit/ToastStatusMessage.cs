// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ToastStatusMessage
{
    private readonly Lock _showLock = new();
    private bool _shown;

    public virtual StatusMessage Message { get; init; }

    public virtual int Duration { get; init; } = 2500;

    public ToastStatusMessage(StatusMessage message)
    {
        Message = message;
    }

    public ToastStatusMessage(string text)
    {
        Message = new StatusMessage() { Message = text };
    }

    public void Show()
    {
        lock (_showLock)
        {
            if (!_shown)
            {
                ExtensionHost.ShowStatus(Message, StatusContext.Extension);
                _ = Task.Run(() =>
                {
                    Thread.Sleep(Duration);

                    lock (_showLock)
                    {
                        _shown = false;
                        ExtensionHost.HideStatus(Message);
                    }
                });
                _shown = true;
            }
        }
    }
}
