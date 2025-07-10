using Bootstrapper.Models.State;
using Bootstrapper.ViewModels.Util;
using System;

namespace Bootstrapper.ViewModels
{
  internal class ProgressViewModel : ViewModelBase
  {
    private string _message;
    private string _package;
    private int _progress;

    public string Message
    {
      get => _message;
      set
      {
        if (_message == value)
          return;

        _message = value;
        base.OnPropertyChanged();
      }
    }

    public string Package
    {
      get => _package;
      set
      {
        if (_package == value)
          return;

        _package = value;
        base.OnPropertyChanged();
        if (string.IsNullOrWhiteSpace(_package))
          Message = string.Empty;
        else
          Message = $"Processing: {_package}";
      }
    }

    public int Progress
    {
      get => _progress;
      set
      {
        if (Math.Abs(_progress - value) < 0.0001)
          return;

        _progress = value;
        base.OnPropertyChanged();
      }
    }

    public void ProcessProgressReport(ProgressReport report)
    {
      Progress = report.Progress;

      if (report.Message != null)
        Message = report.Message;

      if (report.PackageName != null)
        Package = report.PackageName;
    }

    public void Reset()
    {
      Message = string.Empty;
      Package = string.Empty;
      Progress = 0;
    }
  }
}