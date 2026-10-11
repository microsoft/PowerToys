// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace WorkspacesCsharpLibrary.Utils;

public class WorkspaceConflictException : IOException
{
    public WorkspaceConflictException()
        : base("The saved workspace configuration changed since it was loaded.")
    {
    }

    public WorkspaceConflictException(string message)
        : base(message)
    {
    }

    public WorkspaceConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
