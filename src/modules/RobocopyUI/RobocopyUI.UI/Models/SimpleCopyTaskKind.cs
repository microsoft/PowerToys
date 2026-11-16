// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace RobocopyUI.Models;

/// <summary>
/// The copy jobs Simple mode offers. Each value maps to a canonical robocopy switch set.
/// </summary>
public enum SimpleCopyTaskKind
{
    /// <summary>Copy files and folders, including empty ones (<c>/E</c>).</summary>
    CopyFilesAndFolders,

    /// <summary>Copy folders but skip empty ones (<c>/S</c>).</summary>
    CopySkipEmptyFolders,

    /// <summary>Copy only the selected folder, not subfolders.</summary>
    CopyThisFolderOnly,

    /// <summary>Make the destination match the source (<c>/MIR</c>).</summary>
    Mirror,

    /// <summary>Move files and directories, deleting them from the source (<c>/MOVE</c>).</summary>
    MoveEverything,

    /// <summary>Move only files, deleting them from the source (<c>/MOV</c>).</summary>
    MoveFilesOnly,
}
