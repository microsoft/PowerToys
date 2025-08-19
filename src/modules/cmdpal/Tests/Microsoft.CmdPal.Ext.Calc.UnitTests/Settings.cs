// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

public class Settings : ISettingsInterface
{
    private readonly CalculateEngine.TrigMode trigUnit;
    private readonly bool inputUseEnglishFormat;
    private readonly bool outputUseEnglishFormat;
    private readonly bool closeOnEnter;

    public Settings(
        CalculateEngine.TrigMode trigUnit = CalculateEngine.TrigMode.Radians,
        bool inputUseEnglishFormat = false,
        bool outputUseEnglishFormat = false,
        bool closeOnEnter = true)
    {
        this.trigUnit = trigUnit;
        this.inputUseEnglishFormat = inputUseEnglishFormat;
        this.outputUseEnglishFormat = outputUseEnglishFormat;
        this.closeOnEnter = closeOnEnter;
    }

    public CalculateEngine.TrigMode TrigUnit => trigUnit;

    public bool InputUseEnglishFormat => inputUseEnglishFormat;

    public bool OutputUseEnglishFormat => outputUseEnglishFormat;

    public bool CloseOnEnter => closeOnEnter;
}
