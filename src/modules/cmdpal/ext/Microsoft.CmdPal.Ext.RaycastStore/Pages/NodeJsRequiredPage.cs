// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RaycastStore.Pages;

internal sealed partial class NodeJsRequiredPage : ContentPage
{
    private static readonly MarkdownContent _content = new()
    {
        Body = @"# Node.js Required

Raycast extensions are JavaScript/TypeScript applications that require **Node.js** to run.
To browse and install Raycast extensions, please install Node.js first.

---

## Installation Methods

### Option 1: WinGet (Recommended)

Open a terminal and run:

```
winget install OpenJS.NodeJS.LTS
```

### Option 2: Direct Download

Download the installer from the [official Node.js website](https://nodejs.org/):
- Choose the **LTS** (Long Term Support) version
- Run the `.msi` installer and follow the prompts

### Option 3: Node Version Manager (nvm-windows)

For managing multiple Node.js versions:

```
winget install CoreyButler.NVMforWindows
```

Then open a **new** terminal and run:

```
nvm install lts
nvm use lts
```

---

## After Installing

1. Close and reopen the Command Palette
2. The Raycast Extension Store will automatically detect Node.js
3. You'll be able to browse and install extensions

> **Note:** You may need to restart PowerToys or your terminal for PATH changes to take effect.",
    };

    public NodeJsRequiredPage()
    {
        Icon = Icons.WarningIcon;
        Name = "Node.js Required";
        Title = "Node.js Required";
        Commands = new IContextItem[]
        {
            new CommandContextItem(
                "Re-check Node.js",
                "Check if Node.js is now installed",
                "Recheck",
                () =>
                {
                    NodeJsDetector.Reset();
                    Title = "Checking...";
                },
                CommandResult.KeepOpen()),
        };
    }

    public override IContent[] GetContent()
    {
        return new IContent[] { _content };
    }
}
