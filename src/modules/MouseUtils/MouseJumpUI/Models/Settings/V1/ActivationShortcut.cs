// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJumpUI.Models.Settings.V1;

internal sealed class ActivationShortcut
{
    public ActivationShortcut(
        bool? win,
        bool? ctrl,
        bool? alt,
        bool? shift,
        int? code,
        string? key)
    {
        this.Win = win;
        this.Ctrl = ctrl;
        this.Alt = alt;
        this.Shift = shift;
        this.Code = code;
        this.Key = key;
    }

    public bool? Win
    {
        get;
    }

    public bool? Ctrl
    {
        get;
    }

    public bool? Alt
    {
        get;
    }

    public bool? Shift
    {
        get;
    }

    public int? Code
    {
        get;
    }

    public string? Key
    {
        get;
    }
}
