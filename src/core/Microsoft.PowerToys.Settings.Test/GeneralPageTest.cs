using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.Test
{
    [TestClass]
    public class GeneralPageTest
    {
        public const string settings = "{\"packaged\":false,\"startup\":false,\"enabled\":{\"FancyZones\":true,\"ImageResizer\":true,\"FileExplorerPreview\":false,\"PowerRename\":true,\"ShortcutGuide\":false},\"is_elevated\":false,\"run_elevated\":false,\"theme\":\"dark\",\"system_theme\":\"light\",\"powertoys_version\":\"v0.15.3\"}";

        [TestMethod]
        public void GetValue_ShouldReturnKeyValye_WhenSucessful()
        {
            /*
            // arrange
            GeneralPage gp = new GeneralPage();
            dynamic dynamicSettings = Newtonsoft.Json.JsonConvert.DeserializeObject(jsonSettingsString);
            var expectedValue = "dark";

            // act
            var actualValue = gp.GetValue(settings);

            // assert

            */
        }
    }
}
