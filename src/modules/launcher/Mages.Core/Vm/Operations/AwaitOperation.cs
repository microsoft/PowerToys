namespace Mages.Core.Vm.Operations
{
    using Mages.Core.Runtime;
    using System;

    sealed class AwaitOperation : IOperation
    {
        public static readonly IOperation Instance = new AwaitOperation();

        private AwaitOperation()
        {
        }

        public void Invoke(IExecutionContext context)
        {
            var value = context.Pop();
            var promise = value as Future;

            if (promise != null)
            {
                if (!promise.IsCompleted)
                {
                    var continuation = new Future();
                    var position = context.Position;
                    context.Pause();
                    context.Push(continuation);

                    promise.SetCallback((result, error) =>
                    {
                        Conclude(promise, context);
                        context.Position = position + 1;

                        try
                        {
                            (context as ExecutionContext).Execute();
                            continuation.SetResult(context.Pop());
                        }
                        catch (Exception ex)
                        {
                            continuation.SetError(ex.Message);
                        }
                    });
                }
                else
                {
                    Conclude(promise, context);
                }
            }
            else
            {
                context.Push(value);
            }
        }

        private void Conclude(Future promise, IExecutionContext context)
        {
            var error = promise.Error;

            if (error != null)
                throw new Exception(error);

            context.Push(promise.Result);
        }

        public override String ToString()
        {
            return "await";
        }
    }
}
