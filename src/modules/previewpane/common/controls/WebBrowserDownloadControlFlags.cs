// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

/// <summary>
/// Todo.
/// </summary>
[Flags]
public enum WebBrowserDownloadControlFlags : uint
{
    /// <summary>
    /// Too.
    /// </summary>
    DLIMAGES = 0x00000010,

    /// <summary>
    /// To update.
    /// </summary>
    VIDEOS = 0x00000020,

    /// <summary>
    /// Block sounds.
    /// </summary>
    BGSOUNDS = 0x00000040,

    /// <summary>
    /// Block scripts.
    /// </summary>
    NO_SCRIPTS = 0x00000080,

    /// <summary>
    /// Block Java.
    /// </summary>
    NO_JAVA = 0x00000100,

    /// <summary>
    /// Block Ex.
    /// </summary>
    NO_RUNACTIVEXCTLS = 0x00000200,

    /// <summary>
    /// block.
    /// </summary>
    NO_DLACTIVEXCTLS = 0x00000400,

    /// <summary>
    /// tod.
    /// </summary>
    DOWNLOADONLY = 0x00000800,

    /// <summary>
    /// Todo.
    /// </summary>
    NO_FRAMEDOWNLOAD = 0x00001000,

    /// <summary>
    /// Todo.
    /// </summary>
    RESYNCHRONIZE = 0x00002000,

    /// <summary>
    /// todo.
    /// </summary>
    PRAGMA_NO_CACHE = 0x00004000,

    /// <summary>
    /// todo.
    /// </summary>
    NO_BEHAVIORS = 0x00008000,

    /// <summary>
    /// todo.
    /// </summary>
    NO_METACHARSET = 0x00010000,

    /// <summary>
    /// todo.
    /// </summary>
    URL_ENCODING_DISABLE_UTF8 = 0x00020000,

    /// <summary>
    /// todo.
    /// </summary>
    URL_ENCODING_ENABLE_UTF8 = 0x00040000,

    /// <summary>
    /// todo.
    /// </summary>
    NOFRAMES = 0x00080000,

    /// <summary>
    /// todo.
    /// </summary>
    FORCEOFFLINE = 0x10000000,

    /// <summary>
    /// todo.
    /// </summary>
    NO_CLIENTPULL = 0x20000000,

    /// <summary>
    /// todo.
    /// </summary>
    SILENT = 0x40000000,

    /// <summary>
    /// todo.
    /// </summary>
    OFFLINEIFNOTCONNECTED = 0x80000000,

    /// <summary>
    /// todo.
    /// </summary>
    OFFLINE = OFFLINEIFNOTCONNECTED,
}
