using System.Windows.Input;

namespace Bootstrapper.ViewModels.Util
{
  /// <summary>
  ///   An <see cref="ICommand" /> which does not require data passed to the Execute and CanExecute methods.
  /// </summary>
  public interface IDelegateCommand : ICommand
  {
    /// <summary>
    ///   If true then the framework will not automatically query <see cref="ICommand.CanExecute" />.
    ///   Queries can be triggered manually by calling <see cref="RaiseCanExecuteChanged" />.
    /// </summary>
    bool IsAutomaticRequeryDisabled { get; set; }

    /// <summary>
    ///   Method to determine if the command can be executed.
    /// </summary>
    bool CanExecute();

    /// <summary>
    ///   Execution of the command.
    /// </summary>
    void Execute();

    /// <summary>
    ///   Raises the <see cref="ICommand.CanExecuteChanged" /> event.
    /// </summary>
    void RaiseCanExecuteChanged();
  }

  /// <summary>
  ///   A strongly typed <see cref="ICommand" />.
  /// </summary>
  /// <typeparamref name="T">
  ///   Type of data passed to the <see cref="ICommand.Execute" /> and <see cref="ICommand.CanExecute" /> methods.
  /// </typeparamref>
  public interface IDelegateCommand<T> : ICommand
  {
    /// <summary>
    ///   If true then the framework will not automatically query <see cref="ICommand.CanExecute" />.
    ///   Queries can be triggered manually by calling <see cref="RaiseCanExecuteChanged" />.
    /// </summary>
    bool IsAutomaticRequeryDisabled { get; set; }

    /// <summary>
    ///   Method to determine if the command can be executed.
    /// </summary>
    /// <param name="parameter">
    ///   Data to help determine if the command can execute.
    /// </param>
    /// <typeparamref name="T">
    ///   Type of the data passed.
    /// </typeparamref>
    bool CanExecute(T parameter);

    /// <summary>
    ///   Execution of the command.
    /// </summary>
    /// <param name="parameter">
    ///   Data required to execute the command.
    /// </param>
    /// <typeparamref name="T">
    ///   Type of the data passed.
    /// </typeparamref>
    void Execute(T parameter);

    /// <summary>
    ///   Raises the <see cref="ICommand.CanExecuteChanged" /> event.
    /// </summary>
    void RaiseCanExecuteChanged();
  }
}