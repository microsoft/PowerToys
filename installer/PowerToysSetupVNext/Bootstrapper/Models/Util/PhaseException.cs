using System;

namespace Bootstrapper.Models.Util
{
  /// <summary>
  ///   Exception raised when executing a phase
  /// </summary>
  internal class PhaseException : Exception
  {
    public PhaseException(Exception innerException)
      : base(innerException.Message, innerException)
    {
      HResult = innerException.HResult;
    }
  }
}