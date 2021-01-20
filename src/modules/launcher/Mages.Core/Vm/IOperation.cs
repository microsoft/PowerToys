namespace Mages.Core.Vm
{
    /// <summary>
    /// Represents the core interface of an interpreted operation.
    /// </summary>
    public interface IOperation
    {
        /// <summary>
        /// Invokes the operation from the execution context.
        /// </summary>
        /// <param name="context">The current context.</param>
        void Invoke(IExecutionContext context);
    }
}
