using Newtonsoft.Json;

namespace Espresso.Shell.Models
{
    public class EspressoSettingsModel
    {
        [JsonProperty("properties")]
        public Properties Properties { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("version")]
        public string Version { get; set; }
    }

    public class Properties
    {
        [JsonProperty("espresso_keep_display_on")]
        public KeepDisplayOn KeepDisplayOn { get; set; }
        [JsonProperty("espresso_mode")]
        public int Mode { get; set; }
        [JsonProperty("espresso_hours")]
        public Hours Hours { get; set; }
        [JsonProperty("espresso_minutes")]
        public Minutes Minutes { get; set; }
    }

    public class KeepDisplayOn
    {
        public bool Value { get; set; }
    }

    public class Hours
    {
        public int Value { get; set; }
    }

    public class Minutes
    {
        public int Value { get; set; }
    }

}
