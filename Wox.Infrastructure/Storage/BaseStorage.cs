using System;
using System.IO;

namespace Wox.Infrastructure.Storage
{
    [Serializable]
    public abstract class BaseStorage<T> : IStorage where T : class, IStorage, new()
    {
        protected string DirectoryPath { get; } = Path.Combine(WoxDirectroy.Executable, "Config");

        protected string FilePath => Path.Combine(DirectoryPath, FileName + FileSuffix);

        protected abstract string FileSuffix { get; }

        protected abstract string FileName { get; }

        private static object locker = new object();

        protected static T serializedObject;

        public event Action<T> AfterLoad;

        protected virtual void OnAfterLoad(T obj)
        {
            Action<T> handler = AfterLoad;
            if (handler != null) handler(obj);
        }

        public static T Instance
        {
            get
            {
                if (serializedObject == null)
                {
                    lock (locker)
                    {
                        if (serializedObject == null)
                        {
                            serializedObject = new T();
                            serializedObject.Load();
                        }
                    }
                }
                return serializedObject;
            }
        }

        /// <summary>
        /// if loading storage failed, we will try to load default
        /// </summary>
        /// <returns></returns>
        protected virtual T LoadDefault()
        {
            return new T();
        }

        protected abstract void LoadInternal();
        protected abstract void SaveInternal();

        public void Load()
        {
            if (!File.Exists(FilePath))
            {
                if (!Directory.Exists(DirectoryPath))
                {
                    Directory.CreateDirectory(DirectoryPath);
                }
                File.Create(FilePath).Close();
            }
            LoadInternal();
            OnAfterLoad(serializedObject);
        }

        public void Save()
        {
            lock (locker)
            {
                SaveInternal();
            }
        }
    }
}