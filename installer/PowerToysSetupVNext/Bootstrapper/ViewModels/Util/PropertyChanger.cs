using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bootstrapper.ViewModels.Util
{
  /// <summary>
  ///   A <see cref="INotifyPropertyChanged" /> implementation that provides strongly typed OnPropertyChanged
  ///   implementations.
  /// </summary>
  public abstract class PropertyChanger : INotifyPropertyChanged
  {
    /// <summary>
    ///   Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    ///   Triggers the <see cref="PropertyChanged" /> event when passed the name of a property.
    /// </summary>
    /// <param name="propertyName">
    ///   Name of the property whose value changed.
    /// </param>
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }
}