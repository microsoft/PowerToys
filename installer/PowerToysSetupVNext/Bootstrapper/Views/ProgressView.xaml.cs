using System.Windows;

namespace Bootstrapper.Views
{
  public partial class ProgressView
  {
    public ProgressView()
    {
      InitializeComponent();
    }

    public bool IsProgressVisible
    {
      get => (bool)GetValue(IsProgressVisibleProperty);
      set => SetValue(IsProgressVisibleProperty, value);
    }

    public static readonly DependencyProperty IsProgressVisibleProperty = DependencyProperty
      .Register(
        nameof(IsProgressVisible),
        typeof(bool),
        typeof(ProgressView),
        new PropertyMetadata(default(bool)));
  }
}