// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Common
{
    /// <summary>
    /// Flags to control download and execution in Web Browser Control.
    /// Values of flags are defined in mshtmdid.h in distributed Windows Sdk.
    /// </summary>
    [Flags]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Interop, keeping stuff in sync")]
    public enum WebBrowserDownloadControlFlags : int
    {
        /// <summary>
        /// Images will be downloaded from the server if this flag is set.
        /// </summary>
        DLIMAGES = 0x00000010,

        /// <summary>
        /// Videos will be downloaded from the server if this flag is set.
        /// </summary>
        VIDEOS = 0x00000020,

        /// <summary>
        /// Background sounds will be downloaded from the server if this flag is set.
        /// </summary>
        BGSOUNDS = 0x00000040,

        /// <summary>
        /// Scripts will not be executed.
        /// </summary>
        NO_SCRIPTS = 0x00000080,

        /// <summary>
        /// Java applets will not be executed.
        /// </summary>
        NO_JAVA = 0x00000100,

        /// <summary>
        /// ActiveX controls will not be executed.
        /// </summary>
        NO_RUNACTIVEXCTLS = 0x00000200,

        /// <summary>
        /// ActiveX controls will not be downloaded.
        /// </summary>
        NO_DLACTIVEXCTLS = 0x00000400,

        /// <summary>
        /// The page will only be downloaded, not displayed.
        /// </summary>
        DOWNLOADONLY = 0x00000800,

        /// <summary>
        ///  WebBrowser Control will download and parse a frameSet, but not the individual frame objects within the frameSet.
        /// </summary>
        NO_FRAMEDOWNLOAD = 0x00001000,

        /// <summary>
        /// The server will be asked for update status. Cached files will be used if the server indicates that the cached information is up-to-date.
        /// </summary>
        RESYNCHRONIZE = 0x00002000,

        /// <summary>
        /// Files will be re-downloaded from the server regardless of the update status of the files.
        /// </summary>
        PRAGMA_NO_CACHE = 0x00004000,

        /// <summary>
        /// Behaviors are not downloaded and are disabled in the document.
        /// </summary>
        NO_BEHAVIORS = 0x00008000,

        /// <summary>
        /// Character sets specified in meta elements are suppressed.
        /// </summary>
        NO_METACHARSET = 0x00010000,

        /// <summary>
        /// The browsing component will disable UTF-8 encoding.
        /// </summary>
        URL_ENCODING_DISABLE_UTF8 = 0x00020000,

        /// <summary>
        /// The browsing component will enable UTF-8 encoding.
        /// </summary>
        URL_ENCODING_ENABLE_UTF8 = 0x00040000,

        /// <summary>
        /// No Documentation Available.
        /// </summary>
        NOFRAMES = 0x00080000,

        /// <summary>
        /// WebBrowser Control always operates in offline mode.
        /// </summary>
        FORCEOFFLINE = 0x10000000,

        /// <summary>
        /// No client pull operations will be performed.
        /// </summary>
        NO_CLIENTPULL = 0x20000000,

        /// <summary>
        /// No user interface will be displayed during downloads.
        /// </summary>
        SILENT = 0x40000000,
    }
}
