using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using Bootstrapper.ViewModels.Util;

namespace Bootstrapper.ViewModels
{
  internal class ConfigViewModel : ViewModelBase
  {
    private readonly Model _model;

    public ConfigViewModel(Model model)
    {
      _model = model;
    }

    /// <summary>
    ///   An example exposing a bundle variable to the UI.
    /// </summary>
    public string SampleOption
    {
      get
      {
        if (_model.Engine.ContainsVariable("SampleOption"))
          return _model.Engine.GetVariableString("SampleOption");

        return string.Empty;
      }
      set
      {
        _model.Engine.SetVariableString("SampleOption", value, false);
        OnPropertyChanged();
      }
    }

    public void AfterDetect()
    {
      if (_model.State.RelatedBundleStatus != BundleStatus.NotInstalled)
        SampleOption = "Installed";
    }
  }
}