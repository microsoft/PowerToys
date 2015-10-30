using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Newtonsoft.Json;

namespace Wox.Infrastructure.Storage
{
    [Serializable]
    public abstract class BaseStorage<T> : IStorage where T : class,IStorage, new()
    {
        protected abstract string ConfigFolder { get; }

        protected string ConfigPath
        {
            get
            {
                return Path.Combine(ConfigFolder, ConfigName + FileSuffix);
            }
        }

        protected abstract string FileSuffix { get; }

        protected abstract string ConfigName { get; }

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
            if (!File.Exists(ConfigPath))
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }
                File.Create(ConfigPath).Close();
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