using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Wox.Infrastructure.UserSettings;

namespace Wox.Infrastructure
{
    [Serializable]
    public class CommonStorage
    {
        private static string configPath = Directory.GetCurrentDirectory() + "\\data.json";
        private static object locker = new object();
        private static CommonStorage storage;

        public UserSetting UserSetting { get; set; }
        public UserSelectedRecords UserSelectedRecords { get; set; }

        private CommonStorage()
        {
            UserSetting = new UserSetting();
            UserSelectedRecords = new UserSelectedRecords();
        }

        public void Save()
        {
            lock (locker)
            {
                //json is a good choise, readable and flexiable
                string json = JsonConvert.SerializeObject(storage, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
        }

        private static void Load()
        {
            if (!File.Exists(configPath))
            {
                File.Create(configPath).Close();
            }
            string json = File.ReadAllText(configPath);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    storage = JsonConvert.DeserializeObject<CommonStorage>(json);
                }
                catch (Exception e)
                {
                    LoadDefaultUserSetting();
                }
            }
            else
            {
                LoadDefaultUserSetting();
            }
        }

        private static void LoadDefaultUserSetting()
        {
            //default setting
            Instance.UserSetting.Theme = "Default";
            Instance.UserSetting.ReplaceWinR = true;
            Instance.UserSetting.WebSearches = Instance.UserSetting.LoadDefaultWebSearches();
            Instance.UserSetting.Hotkey = "Alt + Space";
        }

        public static CommonStorage Instance
        {
            get
            {
                if (storage == null)
                {
                    lock (locker)
                    {
                        if (storage == null)
                        {
                            storage = new CommonStorage();
                            Load();
                        }
                    }
                }
                return storage;
            }
        }
    }
}