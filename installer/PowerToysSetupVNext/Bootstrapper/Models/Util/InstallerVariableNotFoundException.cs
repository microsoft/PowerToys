using System;

namespace Bootstrapper.Models.Util
{
  internal class InstallerVariableNotFoundException : Exception
  {
    public InstallerVariableNotFoundException(string variableName)
      : base($"The installer variable \"{variableName}\" could not be found.")
    { }

    public InstallerVariableNotFoundException(string variableName, Exception innerException)
      : base($"The installer variable \"{variableName}\" could not be found.", innerException)
    { }
  }
}