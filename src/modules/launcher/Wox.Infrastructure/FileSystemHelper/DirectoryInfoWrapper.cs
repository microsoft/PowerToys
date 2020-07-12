using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wox.Infrastructure.FileSystemHelper
{
    public class DirectoryInfoWrapper : IDirectoryInfoWrapper
    {
        private readonly DirectoryInfo directoryInfo;
        public DirectoryInfoWrapper(DirectoryInfo directoryInfo) 
        {
            this.directoryInfo = directoryInfo;
        }

        public string FullName 
        { 
            get { return directoryInfo.FullName; }
        }
    }
}
