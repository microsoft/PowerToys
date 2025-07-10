using System;
using System.ComponentModel;
using System.Text;

namespace Bootstrapper.ViewModels.Util
{
  internal abstract class ViewModelBase : PropertyChanger, IDataErrorInfo
  {
    public string this[string propertyName] => GetErrors(propertyName);
    public string Error => GetErrors();


    protected override void OnPropertyChanged(string propertyName = null)
    {
      base.OnPropertyChanged(propertyName);
      base.OnPropertyChanged(nameof(Error));
    }

    /// <summary>
    ///   Override in derived classes to provide data validation.
    /// </summary>
    /// <param name="propertyName">Property name. If not supplied, will evaluate all properties and return all VM errors.</param>
    /// <returns>
    ///   Returns an array of all errors found. If there are no errors, returns either an empty array or
    ///   <see langword="null" />.
    /// </returns>
    protected virtual string[] Validate(string propertyName = null)
    {
      return null;
    }

    private string GetErrors(string propertyName = null)
    {
      var errors = Validate(propertyName);
      if (errors == null || errors.Length == 0)
        return string.Empty;

      var sb = new StringBuilder();
      foreach (var error in errors)
      {
        if (sb.Length > 0)
          sb.Append(Environment.NewLine);

        sb.Append(error);
      }

      return sb.ToString();
    }
  }
}