// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Hopper
{
    internal static class CreateNewFolder
    {
        public static FileTransferingStatus[] NewFolderWithFiles(string[] files, string destinationFolder)
        {
            if (!Directory.Exists(destinationFolder))
            {
                return new FileTransferingStatus[] { new FileTransferingStatus(StatusType.DirectoryNotFound, destinationFolder) };
            }

            List<FileTransferingStatus> fileStatus = new();
            foreach (string file in files)
            {
                try
                {
                    File.Move(file, destinationFolder + "\\" + Path.GetFileName(file));
                    fileStatus.Add(new FileTransferingStatus(StatusType.Ok, file));
                }
                catch (FileNotFoundException)
                {
                    fileStatus.Add(new FileTransferingStatus(StatusType.FileNotFound, file));
                }
                catch (UnauthorizedAccessException)
                {
                    fileStatus.Add(new FileTransferingStatus(StatusType.FileProtected, file));
                }
                catch
                {
                    fileStatus.Add(new FileTransferingStatus(StatusType.Undetermined, file));
                }
            }

            return fileStatus.ToArray();
        }
    }
}
