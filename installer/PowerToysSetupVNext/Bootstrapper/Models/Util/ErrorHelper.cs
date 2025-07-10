using System.ComponentModel;

namespace Bootstrapper.Models.Util
{
  public static class ErrorHelper
  {
    /// <summary>
    ///   WiX return code which indicates cancellation.
    /// </summary>
    public const int CancelCode = 1223;

    /// <summary>
    ///   ERROR_INSTALL_USEREXIT (0x80070642)
    /// </summary>
    public const int CancelHResult = -2147023294;


    public static bool HResultIsFailure(int hResult)
    {
      return hResult < 0;
    }

    /// <summary>
    ///   Converts an HRESULT to a win32 error.
    /// </summary>
    /// <param name="hResult"></param>
    /// <returns></returns>
    public static int HResultToWin32(int hResult)
    {
      var win32 = hResult;
      if ((win32 & 0xFFFF0000) == 0x80070000)
        win32 &= 0xFFFF;

      return win32;
    }

    /// <summary>
    ///   Converts an HRESULT to a message.
    /// </summary>
    /// <param name="hResult"></param>
    /// <returns></returns>
    public static string HResultToMessage(int hResult)
    {
      if (hResult == 0)
        return "OK";

      if (HResultIsFailure(hResult))
      {
        var message = new Win32Exception(hResult).Message;
        return $"0x{hResult:X} {message}";
      }

      return $"Result {hResult} (0x{hResult:X})";
    }
  }
}