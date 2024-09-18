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

namespace SamplePagesExtension;

internal sealed partial class YouTubeVideoInfoMarkdownPage : MarkdownPage
{
    private readonly string _markdown = @"
# Markdown Guide

Markdown is a lightweight markup language with plain text formatting syntax. It's often used to format readme files, for writing messages in online forums, and to create rich text using a simple, plain text editor.

---

## Headings

You can create headings using the `#` symbol, with the number of `#` determining the heading level.

```markdown
# H1 Heading
## H2 Heading
### H3 Heading
#### H4 Heading
";

    public YouTubeVideoInfoMarkdownPage()
    {
        Icon = new("\uE946");
        Name = "See more information";
    }

    public override string[] Bodies()
    {
        return [_markdown];
    }
}
