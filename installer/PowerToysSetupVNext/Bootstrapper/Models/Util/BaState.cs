namespace Bootstrapper.Models.Util
{
  /// <summary>
  ///   The states of installation.
  /// </summary>
  internal enum BaStatus
  {
    /// <summary>
    ///   The BA is starting up.
    /// </summary>
    Initializing,

    /// <summary>
    ///   The BA is busy running the detect phase.
    /// </summary>
    Detecting,

    /// <summary>
    ///   The BA has completed the detect phase and is idle, waiting for the user to start the plan phase.
    /// </summary>
    Waiting,

    /// <summary>
    ///   The BA is busy running the plan phase.
    /// </summary>
    Planning,

    /// <summary>
    ///   The BA is busy running the apply phase.
    /// </summary>
    Applying,

    /// <summary>
    ///   The apply phase has successfully completed and the BA is idle, waiting for the user to exit the app.
    /// </summary>
    Applied,

    /// <summary>
    ///   The user cancelled a phase and the BA is idle, waiting for user input.
    /// </summary>
    Cancelled,

    /// <summary>
    ///   A phase failed and the BA is idle, waiting for user input.
    /// </summary>
    Failed
  }
}