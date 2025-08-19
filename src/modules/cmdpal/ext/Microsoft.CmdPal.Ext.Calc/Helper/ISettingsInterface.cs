// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public interface ISettingsInterface
{
    public CalculateEngine.TrigMode TrigUnit { get; }

    public bool InputUseEnglishFormat { get; }

    public bool OutputUseEnglishFormat { get; }

    public bool CloseOnEnter { get; }
}
