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
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.UI.Windowing;
using SSHKeychainExtension.Commands;
using SSHKeychainExtension.Data;

namespace SSHKeychainExtension;

internal sealed class SampleListPage : ListPage
{
    public SampleListPage()
    {
        Icon = new(string.Empty);
        Name = "SSH Keychain";
    }

    public override ISection[] GetItems()
    {
        return [
            new ListSection()
            {
                Title = "Sample List Page",
                Items = [
                    new ListItem(new NoOpAction()) { Title = "TODO: Implement your extension here" },
                    new ListItem(new NoOpAction()) { Title = "This one has a subtitle too", Subtitle = "Example Subtitle" },
                    new ListItem(new NoOpAction())
                    {
                        Title = "This one has a tag too",
                        Subtitle = "the one with a tag",
                        Tags = [new Tag()
                               {
                                   Text = "Sample Tag",
                               }
                        ],
                    }
                ],
            }
            ];
    }
}
