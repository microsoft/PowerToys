using System;
using System.IO;
using Newtonsoft.Json;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Serialize object using json format.
    /// </summary>
    public class JsonStrorage<T>
    {
        private readonly JsonSerializerSettings _serializerSettings;
        private T _data;
        // need a new directory name
        public const string DirectoryName = "Settings";
        public const string FileSuffix = ".json";
        public string FilePath { get; set; }
        public string DirectoryPath { get; set; }


        internal JsonStrorage()
        {
            // use property initialization instead of DefaultValueAttribute
            // easier and flexible for default value of object
            _serializerSettings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public T Load()
        {
            if (File.Exists(FilePath))
            {
                var searlized = File.ReadAllText(FilePath);
                if (!string.IsNullOrWhiteSpace(searlized))
                {
                    Deserialize(searlized);
                }
                else
                {
                    LoadDefault();
                }
            }
            else
            {
                LoadDefault();
            }
            return _data;
        }

        private void Deserialize(string searlized)
        {
            try
            {
                _data = JsonConvert.DeserializeObject<T>(searlized, _serializerSettings);
            }
            catch (JsonSerializationException e)
            {
                LoadDefault();
                Log.Exception($"|JsonStrorage.Deserialize|Deserialize error for json <{FilePath}>", e);
            }
        }

        public void LoadDefault()
        {
            _data = JsonConvert.DeserializeObject<T>("{}", _serializerSettings);
            Save();
        }

        public void Save()
        {
            string serialized = JsonConvert.SerializeObject(_data, Formatting.Indented);
            File.WriteAllText(FilePath, serialized);
        }
    }
}
