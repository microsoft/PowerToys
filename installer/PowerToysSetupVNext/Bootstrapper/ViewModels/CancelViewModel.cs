using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using Bootstrapper.ViewModels.Util;

namespace Bootstrapper.ViewModels
{
  internal class CancelViewModel : ViewModelBase
  {
    private readonly Model _model;

    public CancelViewModel(Model model)
    {
      _model = model;
      CancelCommand = new DelegateCommand(Cancel, CanCancel);
    }

    public IDelegateCommand CancelCommand { get; }

    private void Cancel()
    {
      _model.State.CancelRequested = true;
    }

    private bool CanCancel()
    {
      return !_model.State.CancelRequested && (_model.State.BaStatus == BaStatus.Planning || _model.State.BaStatus == BaStatus.Applying);
    }
  }
}