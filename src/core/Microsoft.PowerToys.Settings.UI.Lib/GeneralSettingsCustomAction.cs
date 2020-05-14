using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class GeneralSettingsCustomAction
    {
        [JsonPropertyName("action")]
        public OutGoingGeneralSettings GeneralSettingsAction { get; set; }

        public GeneralSettingsCustomAction()
        {
        }

        public GeneralSettingsCustomAction(OutGoingGeneralSettings action)
        {
            GeneralSettingsAction = action;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
