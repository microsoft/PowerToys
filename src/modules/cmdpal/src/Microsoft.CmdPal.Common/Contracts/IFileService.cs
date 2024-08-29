// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.CmdPal.Common.Contracts;

public interface IFileService
{
    T Read<T>(string folderPath, string fileName);

    void Save<T>(string folderPath, string fileName, T content);

    void Delete(string folderPath, string fileName);
}
