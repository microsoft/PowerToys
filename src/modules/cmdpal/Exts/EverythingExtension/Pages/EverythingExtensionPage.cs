// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.UI.ApplicationSettings;
using static EverythingExtension.NativeMethods;

namespace EverythingExtension;

internal sealed partial class EverythingExtensionPage : DynamicListPage
{
    public EverythingExtensionPage()
    {
        Icon = new(File.Exists("C:\\Program Files\\Everything\\Everything.exe") ?
            "C:\\Program Files\\Everything\\Everything.exe" :
            "C:\\Program Files (x86)\\Everything\\Everything.exe"
        );
        Name = "Everything";

        Everything_SetRequestFlags(Request.FILE_NAME | Request.PATH);
        Everything_SetSort(Sort.NAME_ASCENDING);
        Everything_SetMax(20);
    }

    public override ISection[] GetItems(string query)
    {
        Everything_SetSearchW(query);

        if (!Everything_QueryW(true))
        {
            // Throwing an exception would make sense, however,
            // WinRT & COM totally eat any exception info.
            // var e = new Win32Exception("Unable to Query");
            var lastError = Everything_GetLastError();
            var message = lastError switch
            {
                (uint)EverythingErrors.EVERYTHING_OK => "The operation completed successfully",
                (uint)EverythingErrors.EVERYTHING_ERROR_MEMORY => "Failed to allocate memory for the search query",
                (uint)EverythingErrors.EVERYTHING_ERROR_IPC => "IPC is not available",
                (uint)EverythingErrors.EVERYTHING_ERROR_REGISTERCLASSEX => "Failed to register the search query window class",
                (uint)EverythingErrors.EVERYTHING_ERROR_CREATEWINDOW => "Failed to create the search query window",
                (uint)EverythingErrors.EVERYTHING_ERROR_CREATETHREAD => "Failed to create the search query thread",
                (uint)EverythingErrors.EVERYTHING_ERROR_INVALIDINDEX => "Invalid index.The index must be greater or equal to 0 and less than the number of visible results",
                (uint)EverythingErrors.EVERYTHING_ERROR_INVALIDCALL => "Invalid call",
                _ => "Unexpected error",
            };
            List<ListItem> items = new List<ListItem>();
            items.Add(new ListItem(new NoOpCommand() { Name = "Failed to query. Error was:" }));
            items.Add(new ListItem(new NoOpCommand()) { Title = message, Subtitle = $"0x{lastError:X8}" });
            if (lastError == (uint)EverythingErrors.EVERYTHING_ERROR_IPC)
            {
                items.Add(new ListItem(new NoOpCommand() { Name = "(Are you sure Everything is running?)" }));
            }

            return [
                new ListSection()
                {
                    Items = items.ToArray(),
                }
            ];
        }

        var resultCount = Everything_GetNumResults();

        // Create a new ListSections
        var section = new ListSection();

        // Create a List to store ListItems
        var itemList = new List<ListItem>();

        // Loop through the results and add them to the List
        for (uint i = 0; i < resultCount; i++)
        {
            // Get the result file name
            var fileName = Marshal.PtrToStringUni(Everything_GetResultFileNameW(i));

            // Get the result file path
            var filePath = Marshal.PtrToStringUni(Everything_GetResultPathW(i));

            // Concatenate the file path and file name
            var fullName = Path.Combine(filePath, fileName);

            // System.Drawing.Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(fullTitle);
            itemList.Add(new ListItem(new OpenFileCommand(fullName, filePath))
            {
                Title = fileName,
                Subtitle = filePath,
                MoreCommands = [
                    new CommandContextItem(new OpenExplorerCommand(fullName)),
                    new CommandContextItem(new CopyPathCommand(fullName)),
                ],
            });
        }

        // Convert the List to an array and assign it to the Items property
        section.Items = itemList.ToArray();

        // Return the ListSection with the items
        return [section];
    }
}
