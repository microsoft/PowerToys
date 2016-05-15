using System.IO;
using Newtonsoft.Json;
using Wox.Infrastructure;
using Wox.Infrastructure.Logger;

namespace Wox.Infrastructure.Storage
{
    /// <summary>
    /// Serialize object using json format.
    /// </summary>
    public class JsonStrorage<T> : Storage<T> where T : new()
    {
        private readonly JsonSerializerSettings _serializerSettings;

        internal JsonStrorage()
        {
            FileSuffix = ".json";
            DirectoryName = Wox.Settings;
            DirectoryPath = Wox.SettingsPath;
            FilePath = Path.Combine(DirectoryPath, FileName + FileSuffix);

            ValidateDirectory();

            // use property initialization instead of DefaultValueAttribute
            // easier and flexible for default value of object
            _serializerSettings = new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public override T Load()
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
            return Data;
        }

        private void Deserialize(string searlized)
        {
            try
            {
                Data = JsonConvert.DeserializeObject<T>(searlized, _serializerSettings);
            }
            catch (JsonSerializationException e)
            {
                LoadDefault();
                Log.Exception(e);
            }
        }

        public override void LoadDefault()
        {
            Data = JsonConvert.DeserializeObject<T>("{}", _serializerSettings);
            Save();
        }

        public override void Save()
        {
            string serialized = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(FilePath, serialized);
        }
    }
}
