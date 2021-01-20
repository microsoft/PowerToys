namespace Mages.Core.Tests
{
    using Mages.Core.Tests.Mocks;
    using NUnit.Framework;
    using System;
    using System.Threading.Tasks;

    [TestFixture]
    public class FutureTests
    {
        [Test]
        public void CalculateAsyncFunctionInForLoop()
        {
            Test(@"var sum = 0.0;

for (var i = 0; i < 5; ++i) {
    sum += await calcAsync(10);
}

sum
", 15.0, engine => engine.SetFunction("calcAsync", FutureMock.Number(3.0)));
        }

        [Test]
        public void CalculateAsyncInFunction()
        {
            Test(@"var f = x => {
  var y = await calcAsync(20);
  return x + y;
};

f(5)", 10.0, engine => engine.SetFunction("calcAsync", FutureMock.Number(5.0)));
        }

        [Test]
        public void CalculateMultipleAsyncInFunction()
        {
            Test(@"var f = x => {
  var y = await barAsync(20);
  var z = await fooAsync(5);
  return x + y * z;
};

f(5)", 80, engine =>
            {
                engine.SetFunction("barAsync", FutureMock.Number(5.0));
                engine.SetFunction("fooAsync", FutureMock.Number(15.0));
            });
        }

        private static void Test(String sourceCode, Object expected, Action<Engine> setup)
        {
            var engine = new Engine();
            setup.Invoke(engine);
            var result = engine.InterpretAsync(sourceCode);
            var tcs = new TaskCompletionSource<Object>();
            result.SetCallback((value, error) => tcs.SetResult(value));
            Assert.AreEqual(expected, tcs.Task.Result);
        }
    }
}
