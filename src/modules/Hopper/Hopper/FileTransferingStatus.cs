// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Hopper
{
    public class FileTransferingStatus
    {
        private StatusType _errorType;

        public StatusType ErrorType
        {
            get
            {
                return _errorType;
            }

            set
            {
                _errorType = value;
            }
        }

        private string? _fileName;

        public string? FileName
        {
            get
            {
                return _fileName;
            }

            set
            {
                _fileName = value;
            }
        }

        public FileTransferingStatus(StatusType errorType, string fileName)
        {
            _errorType = errorType;
            _fileName = fileName;
        }
    }

    public enum StatusType
    {
        Ok,
        DirectoryNotFound,
        FileNotFound,
        FileProtected,
        FileTwice,
        Undetermined,
    }
}
