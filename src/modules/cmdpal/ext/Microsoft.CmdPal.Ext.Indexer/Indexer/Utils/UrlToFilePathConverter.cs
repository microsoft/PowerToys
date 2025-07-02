// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.Utils;

public class UrlToFilePathConverter
{
    public static string Convert(string url)
    {
        var result = url.Replace('/', '\\'); // replace all '/' to '\'

        var fileProtocolString = "file:";
        var indexProtocolFound = url.IndexOf(fileProtocolString, StringComparison.CurrentCultureIgnoreCase);

        if (indexProtocolFound != -1 && (indexProtocolFound + fileProtocolString.Length) < url.Length)
        {
            result = result[(indexProtocolFound + fileProtocolString.Length)..];
        }

        return result;
    }
}
