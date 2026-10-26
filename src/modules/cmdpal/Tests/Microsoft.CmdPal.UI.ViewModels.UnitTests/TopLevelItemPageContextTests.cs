// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class TopLevelItemPageContextTests
{
    private sealed class ThrowingViewModel(IPageContext context, Exception exception) : ExtensionObjectViewModel(context)
    {
        public override void InitializeProperties() => throw exception;
    }

    [TestMethod]
    public void InitializeProperties_FailureLogsExceptionAndProviderContextAtErrorLevel()
    {
        var originalLogger = CoreLogger.Instance;
        var originalSynchronizationContext = SynchronizationContext.Current;
        var logger = new Mock<ILogger>();
        CoreLogger.InitializeLogger(logger.Object);
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

        try
        {
            var provider = Mock.Of<ICommandProvider>(model => model.Id == "test.provider");
            var wrapper = new CommandProviderWrapper(provider, TaskScheduler.Default);
            var exception = new InvalidOperationException("Initialization failed", new ArgumentException("Inner failure"));
            var viewModel = new ThrowingViewModel(wrapper.TopLevelPageContext, exception);

            viewModel.SafeInitializePropertiesSynchronous();

            logger.Verify(
                log => log.LogError(
                    It.Is<string>(message => message.StartsWith("[Host message]", StringComparison.Ordinal)
                        && message.Contains("TopLevelItemPageContext(test.provider)", StringComparison.Ordinal)),
                    exception,
                    nameof(TopLevelItemPageContext.ShowException),
                    It.Is<string>(path => path.EndsWith("TopLevelItemPageContext.cs", StringComparison.Ordinal)),
                    It.Is<int>(line => line > 0)),
                Times.Once);
            Assert.IsNotNull(exception.StackTrace);
            StringAssert.Contains(exception.StackTrace, nameof(ThrowingViewModel.InitializeProperties));
        }
        finally
        {
            CoreLogger.InitializeLogger(originalLogger!);
            SynchronizationContext.SetSynchronizationContext(originalSynchronizationContext);
        }
    }
}
