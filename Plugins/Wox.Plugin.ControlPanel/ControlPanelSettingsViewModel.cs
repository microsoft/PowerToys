namespace Wox.Plugin.ControlPanel
{
    public class ControlPanelSettingsViewModel : BaseModel
    {
        private readonly Settings _settings;

        public ControlPanelSettingsViewModel(Settings settings)
        {
            _settings = settings;
        }

        public bool ShouldUsePinYin
        {
            get { return _settings.ShouldUsePinYin; }
            set
            {
                _settings.ShouldUsePinYin = value;
                this.OnPropertyChanged();
            }
        }
    }
}