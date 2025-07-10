using Bootstrapper.Models;
using Bootstrapper.Models.Util;
using System;
using WixToolset.BootstrapperApplicationApi;

namespace Bootstrapper;

internal class BootstrapperApp : BootstrapperApplication
{
  private Model _model;

  public int ExitCode { get; private set; }

  protected override void OnCreate(CreateEventArgs args)
  {
    base.OnCreate(args);

    try
    {
      var factory = new WpfBaFactory();
      _model = factory.Create(this, args.Engine, args.Command);
    }
    catch (Exception ex)
    {
      ExitCode = ErrorHelper.HResultToWin32(ex.HResult);
      args.Engine.Log(LogLevel.Error, ex.ToString());
      throw;
    }
  }

  protected override void Run()
  {
    var hResult = 0;
    try
    {
      _model.Log.Write("Running bootstrapper application.");

      try
      {
        _model.UiFacade.Initialize(_model);
        _model.Engine.Detect();
        _model.UiFacade.RunMessageLoop();
      }
      finally
      {
        hResult = _model.State.PhaseResult;
      }
    }
    catch (Exception ex)
    {
      hResult = ex.HResult;
      _model.Log.Write(ex);
    }
    finally
    {
      // If the HRESULT is an error, convert it to a win32 error code
      ExitCode = ErrorHelper.HResultToWin32(hResult);
      _model.SaveEmbeddedLog(ExitCode);
      _model.Engine.Quit(ExitCode);
    }
  }
}