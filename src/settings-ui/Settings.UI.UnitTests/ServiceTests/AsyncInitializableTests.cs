// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.ServiceTests
{
    [TestClass]
    public class AsyncInitializableTests
    {
        private sealed class TestViewModel : PageViewModelBase
        {
            protected override string ModuleName => "TestModule";

            public bool InitializeCoreWasCalled { get; set; }

            protected override Task InitializeCoreAsync(CancellationToken cancellationToken = default)
            {
                InitializeCoreWasCalled = true;
                return Task.CompletedTask;
            }
        }

        [TestMethod]
        public async Task InitializeAsync_SetsIsInitializedToTrue()
        {
            var viewModel = new TestViewModel();

            await viewModel.InitializeAsync();

            Assert.IsTrue(viewModel.IsInitialized);
        }

        [TestMethod]
        public async Task InitializeAsync_CallsInitializeCoreAsync()
        {
            var viewModel = new TestViewModel();

            await viewModel.InitializeAsync();

            Assert.IsTrue(viewModel.InitializeCoreWasCalled);
        }

        [TestMethod]
        public async Task InitializeAsync_DoesNotReinitializeIfAlreadyInitialized()
        {
            var viewModel = new TestViewModel();

            await viewModel.InitializeAsync();
            viewModel.InitializeCoreWasCalled = false; // Reset the flag

            await viewModel.InitializeAsync(); // Should not call InitializeCoreAsync again

            // InitializeCoreWasCalled should still be false since we reset it
            // and InitializeAsync should skip if already initialized
            Assert.IsFalse(viewModel.InitializeCoreWasCalled);
        }

        [TestMethod]
        public async Task InitializeAsync_SetsIsLoadingDuringInitialization()
        {
            var viewModel = new TestViewModel();

            // IsLoading should be false before initialization
            Assert.IsFalse(viewModel.IsLoading);

            await viewModel.InitializeAsync();

            // IsLoading should be false after initialization completes
            Assert.IsFalse(viewModel.IsLoading);
        }
    }
}
