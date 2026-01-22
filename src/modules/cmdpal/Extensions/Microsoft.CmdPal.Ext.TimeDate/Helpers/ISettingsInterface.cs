// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.TimeDate.Helpers;

public interface ISettingsInterface
{
    public int FirstWeekOfYear { get; }

    public int FirstDayOfWeek { get; }

    public bool EnableFallbackItems { get; }

    public bool TimeWithSecond { get; }

    public bool DateWithWeekday { get; }

    public List<string> CustomFormats { get; }
}
