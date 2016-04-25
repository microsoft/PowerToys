using System.IO;
using Newtonsoft.Json;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Serialize object using json format.
    /// </summary>
    public class JsonStrorage<T> where T : new()
    {
        private T _json;
        private readonly JsonSerializerSettings _serializerSettings;

        protected string FileName { get; set; }
        protected string FilePath { get; set; }
        protected const string FileSuffix = ".json";
        protected string DirectoryPath { get; set; }
        protected const string DirectoryName = "Config";

        internal JsonStrorage()
        {
            FileName = typeof(T).Name;
            DirectoryPath = Path.Combine(WoxDirectroy.Executable, DirectoryName);
            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix);

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
            if (!Directory.Exists(DirectoryPath))
            {
                Directory.CreateDirectory(DirectoryPath);
            }

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

            return _json;
        }

        private void Deserialize(string searlized)
        {
            try
            {
                _json = JsonConvert.DeserializeObject<T>(searlized, _serializerSettings);
            }
            catch (JsonSerializationException e)
            {
                LoadDefault();
                Log.Error(e);
            }

        }

        private void LoadDefault()
        {
            _json = JsonConvert.DeserializeObject<T>("{}", _serializerSettings);
            Save();
        }

        public void Save()
        {
            string serialized = JsonConvert.SerializeObject(_json, Formatting.Indented);
            File.WriteAllText(FilePath, serialized);
        }
    }
}
