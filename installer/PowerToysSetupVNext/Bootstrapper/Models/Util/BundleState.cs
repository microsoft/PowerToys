namespace Bootstrapper.Models.Util
{
  /// <summary>
  ///   The states of bundle detection.
  /// </summary>
  internal enum BundleStatus
  {
    Unknown = 0,

    /// <summary>
    ///   There are no upgrade related bundles installed.
    /// </summary>
    NotInstalled = 1,

    /// <summary>
    ///   All upgrade related bundles that are installed are earlier versions than this bundle.
    /// </summary>
    OlderInstalled = 2,

    /// <summary>
    ///   All upgrade related bundles that are installed are the same version as this bundle.
    /// </summary>
    Current = 3,

    /// <summary>
    ///   At least one upgrade related bundle is installed that is a newer version than this bundle.
    /// </summary>
    NewerInstalled = 4
  }
}