using Bootstrapper.Models;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper.Phases;

/// <summary>
///   Manages cancellation
/// </summary>
internal class CancelHandler
{
  private readonly Model _model;

  public CancelHandler(Model model)
  {
    _model = model;
  }

  public void CheckForCancel(object sender, CancellableHResultEventArgs e)
  {
    if (!e.Cancel && _model.State.CancelRequested)
      e.Cancel = true;
  }

  public void CheckResult(object sender, ResultEventArgs e)
  {
    if (e.Result != Result.Abort && e.Result != Result.Error && e.Result != Result.Cancel && _model.State.CancelRequested)
      e.Result = Result.Cancel;
  }
}