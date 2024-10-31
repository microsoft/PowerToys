// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CmdPal.Ext.WindowsServices.Helpers;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Windowing;

namespace Microsoft.CmdPal.Ext.WindowsServices;

internal sealed partial class ServicesListPage : DynamicListPage
{
    public ServicesListPage()
    {
        Icon = new(string.Empty);
        Name = "Windows Services";
    }

    public override ISection[] GetItems(string query)
    {
        ListItem[] items = ServiceHelper.Search(query).ToArray();

        return new ISection[] { new ListSection() { Title = "Windows Services", Items = items } };
    }
}
