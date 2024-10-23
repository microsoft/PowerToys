// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;

namespace WindowsCommandPalette.Views;

public sealed partial class ErrorListItem : ListItem
{
    public ErrorListItem(Exception ex)
        : base(new NoOpAction())
    {
        Title = "Unknown error in extension";
        Subtitle = ex.Message;
        if (ex.StackTrace != null)
        {
            Details = new Details() { Body = FormatStackTrace(ex) };
        }
    }

    private string FormatStackTrace(Exception ex)
    {
        return $"""
Local stack trace:

```
{ex.StackTrace}
```

""";
    }
}
