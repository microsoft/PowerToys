using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Windows.Input;

namespace Bootstrapper.ViewModels.Util
{
  /// <summary>
  ///   An implementation of <see cref="IDelegateCommand" /> which allows delegating the commanding
  ///   logic to methods passed as parameters, and enables a View to bind commands to objects that
  ///   are not part of the element tree.
  /// </summary>
  public sealed class DelegateCommand : BaseCommand, IDelegateCommand
  {
    private readonly Action _executeMethod;
    private readonly Func<bool> _canExecuteMethod;

    /// <summary>
    ///   Initializes a DelegateCommand with methods for execution, verification, and allows specifying
    ///   if the CommandManager's automatic re-query is disabled for this command.
    /// </summary>
    /// <param name="executeMethod">
    ///   Method which is called when the command is executed.
    /// </param>
    /// <param name="canExecuteMethod">
    ///   Method which is called to determine if the Execute method may be run.
    /// </param>
    /// <param name="isAutomaticRequeryDisabled">
    ///   If true then the framework will not automatically query <see cref="ICommand.CanExecute" />.
    ///   Queries can be triggered manually by calling <see cref="BaseCommand.RaiseCanExecuteChanged" />.
    /// </param>
    public DelegateCommand(Action executeMethod, Func<bool> canExecuteMethod = null, bool isAutomaticRequeryDisabled = false)
      : base(isAutomaticRequeryDisabled)
    {
      _executeMethod = executeMethod ?? throw new ArgumentNullException(nameof(executeMethod));
      _canExecuteMethod = canExecuteMethod;
    }

    /// <summary>
    ///   Method to determine if the command can be executed.
    /// </summary>
    [DebuggerStepThrough]
    public bool CanExecute()
    {
      return _canExecuteMethod == null || _canExecuteMethod();
    }

    /// <summary>
    ///   Executes the command.
    /// </summary>
    public void Execute()
    {
      _executeMethod();
    }

    [DebuggerStepThrough]
    bool ICommand.CanExecute(object obj)
    {
      return CanExecute();
    }

    void ICommand.Execute(object obj)
    {
      Execute();
    }

    public DelegateCommand ListenOn<TObservedType, TPropertyType>(TObservedType viewModel, Expression<Func<TObservedType, TPropertyType>> propertyExpression) where TObservedType : INotifyPropertyChanged
    {
      AddListenOn(viewModel, nameof(propertyExpression));
      return this;
    }

    public DelegateCommand ListenOn<TObservedType>(TObservedType viewModel, string propertyName) where TObservedType : INotifyPropertyChanged
    {
      AddListenOn(viewModel, propertyName);
      return this;
    }
  }

  /// <summary>
  ///   A strongly typed implementation of <see cref="IDelegateCommand{T}" /> which allows delegating the commanding
  ///   logic to methods passed as parameters, and enables a View to bind commands to objects that
  ///   are not part of the element tree.
  /// </summary>
  /// <typeparam name="T">Type of the parameter passed to the delegates</typeparam>
  public sealed class DelegateCommand<T> : BaseCommand, IDelegateCommand<T>
  {
    private readonly Action<T> _executeMethod;
    private readonly Func<T, bool> _canExecuteMethod;

    /// <summary>
    ///   Initializes a DelegateCommand with methods for execution, verification, and allows specifying
    ///   if the CommandManager's automatic re-query is disabled for this command.
    /// </summary>
    /// <param name="executeMethod">
    ///   Method which is called when the command is executed.
    /// </param>
    /// <param name="canExecuteMethod">
    ///   Method which is called to determine if the Execute method may be run.
    /// </param>
    /// <param name="isAutomaticRequeryDisabled">
    ///   If true then the framework will not automatically query <see cref="ICommand.CanExecute" />.
    ///   Queries can be triggered manually by calling <see cref="BaseCommand.RaiseCanExecuteChanged" />.
    /// </param>
    /// <typeparamref name="T">
    ///   The type of the data passed to the <see cref="Execute" /> and <see cref="CanExecute" /> methods.
    /// </typeparamref>
    public DelegateCommand(Action<T> executeMethod, Func<T, bool> canExecuteMethod = null, bool isAutomaticRequeryDisabled = false)
      : base(isAutomaticRequeryDisabled)
    {
      _executeMethod = executeMethod ?? throw new ArgumentNullException(nameof(executeMethod));
      _canExecuteMethod = canExecuteMethod;
    }

    /// <summary>
    ///   Method to determine if the command can be executed.
    /// </summary>
    /// <typeparamref name="T">
    ///   Type of the data passed.
    /// </typeparamref>
    [DebuggerStepThrough]
    public bool CanExecute(T parameter)
    {
      return _canExecuteMethod == null || _canExecuteMethod(parameter);
    }

    /// <summary>
    ///   Execution of the command.
    /// </summary>
    /// <typeparamref name="T">
    ///   Type of the data passed.
    /// </typeparamref>
    public void Execute(T parameter)
    {
      _executeMethod(parameter);
    }

    /// <summary>
    ///   Defines the method that determines whether the command can execute in its current state.
    /// </summary>
    /// <returns>
    ///   true if this command can be executed; otherwise, false.
    /// </returns>
    /// <param name="parameter">
    ///   Data used by the command.  If the command does not require data to be passed, this object can
    ///   be set to null.
    /// </param>
    [DebuggerStepThrough]
    bool ICommand.CanExecute(object parameter)
    {
      // if T is of value type and the parameter is not
      // set yet, then return false if CanExecute delegate
      // exists, else return true
      if (parameter == null && typeof(T).IsValueType)
        return _canExecuteMethod == null;

      return CanExecute((T)parameter);
    }

    /// <summary>
    ///   Defines the method to be called when the command is invoked.
    /// </summary>
    /// <param name="parameter">
    ///   Data used by the command.  If the command does not require data to be passed, this object can
    ///   be set to null.
    /// </param>
    void ICommand.Execute(object parameter)
    {
      Execute((T)parameter);
    }

    public DelegateCommand<T> ListenOn<TObservedType, TPropertyType>(TObservedType viewModel, Expression<Func<TObservedType, TPropertyType>> propertyExpression) where TObservedType : INotifyPropertyChanged
    {
      AddListenOn(viewModel, nameof(propertyExpression));
      return this;
    }

    public DelegateCommand<T> ListenOn<TObservedType>(TObservedType viewModel, string propertyName) where TObservedType : INotifyPropertyChanged
    {
      AddListenOn(viewModel, propertyName);
      return this;
    }
  }

  public abstract class BaseCommand
  {
    private bool _isAutomaticRequeryDisabled;
    private List<WeakReference> _canExecuteChangedHandlers;

    /// <summary>
    ///   Initializes a DelegateCommand with methods for execution, verification, and allows specifying
    ///   if the CommandManager's automatic re-query is disabled for this command.
    /// </summary>
    /// <param name="isAutomaticRequeryDisabled">
    ///   If true then the framework will not automatically query <see cref="ICommand.CanExecute" />.
    ///   Queries can be triggered manually by calling <see cref="RaiseCanExecuteChanged" />.
    /// </param>
    protected BaseCommand(bool isAutomaticRequeryDisabled = false)
    {
      _isAutomaticRequeryDisabled = isAutomaticRequeryDisabled;
    }

    /// <summary>
    ///   Occurs when changes occur that affect whether the command should execute.
    /// </summary>
    public event EventHandler CanExecuteChanged
    {
      add
      {
        if (!_isAutomaticRequeryDisabled)
          CommandManager.RequerySuggested += value;

        CommandManagerHelper.AddWeakReferenceHandler(ref _canExecuteChangedHandlers, value, 2);
      }
      remove
      {
        if (!_isAutomaticRequeryDisabled)
          CommandManager.RequerySuggested -= value;

        CommandManagerHelper.RemoveWeakReferenceHandler(_canExecuteChangedHandlers, value);
      }
    }

    /// <summary>
    ///   If true then the framework will not automatically query <see cref="ICommand.CanExecute" />.
    ///   Queries can be triggered manually by calling <see cref="RaiseCanExecuteChanged" />.
    /// </summary>
    public bool IsAutomaticRequeryDisabled
    {
      get => _isAutomaticRequeryDisabled;
      set
      {
        if (_isAutomaticRequeryDisabled == value)
          return;

        if (value)
          CommandManagerHelper.RemoveHandlersFromRequerySuggested(_canExecuteChangedHandlers);
        else
          CommandManagerHelper.AddHandlersToRequerySuggested(_canExecuteChangedHandlers);

        _isAutomaticRequeryDisabled = value;
      }
    }

    /// <summary>
    ///   Raises the <see cref="ICommand.CanExecuteChanged" /> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
      CommandManagerHelper.CallWeakReferenceHandlers(_canExecuteChangedHandlers);
    }


    public void ListenForNotificationFrom<TObservedType>(TObservedType viewModel) where TObservedType : INotifyPropertyChanged
    {
      viewModel.PropertyChanged += OnObservedPropertyChanged;
    }

    protected void AddListenOn<TObservedType, TPropertyType>(TObservedType viewModel, Expression<Func<TObservedType, TPropertyType>> propertyExpression) where TObservedType : INotifyPropertyChanged
    {
      AddListenOn(viewModel, nameof(propertyExpression));
    }

    protected void AddListenOn<TObservedType>(TObservedType viewModel, string propertyName) where TObservedType : INotifyPropertyChanged
    {
      viewModel.PropertyChanged += (sender, e) =>
      {
        if (e.PropertyName == propertyName)
          RaiseCanExecuteChanged();
      };
    }

    private void OnObservedPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      RaiseCanExecuteChanged();
    }
  }
}