using System;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    public class Storage<T>
    {
        protected T Data;
        protected Type DataType { get; }
        public string FileName { get; }
        public string FilePath { get; set; }
        public string FileSuffix { get; set; }
        public string DirectoryPath { get; set; }
        public string DirectoryName { get; set; }

        public virtual T Load()
        {
            throw new NotImplementedException();
        }

        public virtual void Save()
        {
            throw new NotImplementedException();
        }

        public virtual void LoadDefault()
        {
            throw new NotImplementedException();
        }

        protected Storage()
        {
            DataType = typeof (T);
            FileName = DataType.Name;
            DirectoryPath = Constant.DataDirectory;
        }

        protected void ValidateDirectory()
        {
            if (!Directory.Exists(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
            }
        }
    }
}
