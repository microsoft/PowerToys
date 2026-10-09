// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Wox.Plugin.Interfaces
{
    /// <summary>
    /// This interface is to indicate results that contain a file/folder that is available for drag & drop to other applications
    /// </summary>
    public interface IFileDropResult
    {
        public string Path { get; set; }
    }
}
