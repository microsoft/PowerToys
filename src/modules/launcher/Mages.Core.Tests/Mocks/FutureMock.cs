namespace Mages.Core.Tests.Mocks
{
    using Mages.Core.Runtime;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public static class FutureMock
    {
        public static Function Number(Object value)
        {
            return new Function(args =>
            {
                var future = new Future();
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds((Double)args[0]));
                    future.SetResult(value);
                });
                return future;
            });
        }
    }
}
