using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Wox.Infrastructure.Storage
{
    public abstract class BaseStorage<T> where T : class, new()
    {
        private string configFolder = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Config");
        private string fileSuffix = ".json";
        private static object locker = new object();
        private static T storage;

        public event Action<T> AfterLoadConfig;

        protected virtual void OnAfterLoadConfig(T obj)
        {
            Action<T> handler = AfterLoadConfig;
            if (handler != null) handler(obj);
        }

        protected abstract string ConfigName { get; }

        public static T Instance
        {
            get
            {
                if (storage == null)
                {
                    lock (locker)
                    {
                        if (storage == null)
                        {
                            storage = new T();
                            (storage as BaseStorage<T>).Load();
                        }
                    }
                }
                return storage;
            }
        }

        protected virtual T LoadDefaultConfig()
        {
            return storage;
        }

        private void Load()
        {
            string configPath = Path.Combine(configFolder, ConfigName + fileSuffix);
            if (!File.Exists(configPath))
            {
                if (!Directory.Exists(configFolder))
                    Directory.CreateDirectory(configFolder);
                File.Create(configPath).Close();
            }
            string json = File.ReadAllText(configPath);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    storage = JsonConvert.DeserializeObject<T>(json);
                }
                catch (Exception)
                {
                    storage = LoadDefaultConfig();
                }
            }
            else
            {
                storage = LoadDefaultConfig();
            }
            OnAfterLoadConfig(storage);
        }

        public void Save()
        {
            lock (locker)
            {
                //json is a good choise, readable and flexiable
                string configPath = Path.Combine(configFolder, ConfigName + fileSuffix);
                string json = JsonConvert.SerializeObject(storage, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
        }
    }
}
