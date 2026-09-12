// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class PageViewModelTests
{
    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = new();

        protected override IEnumerable<Task>? GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void RunAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
            }
        }
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed class TestPageViewModel : PageViewModel
    {
        public TestPageViewModel(Page page, TaskScheduler scheduler)
            : base(page, scheduler, new TestAppExtensionHost(), CommandProviderContext.Empty)
        {
        }

        public void SetInitialSearchText(string value) => SetInitialSearchTextBox(value);
    }

    [TestMethod]
    public void InitialSearchTextBoxUpdate_IsMarshaledToUiScheduler()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new Page
        {
            Id = "page",
            Name = "Page",
        };
        var viewModel = new TestPageViewModel(page, scheduler);
        var searchTextChanged = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PageViewModel.SearchTextBox))
            {
                searchTextChanged = true;
            }
        };

        viewModel.SetInitialSearchText("ssh");

        Assert.AreEqual("ssh", viewModel.SearchTextBox);
        Assert.IsFalse(searchTextChanged, "The UI-facing notification must not be raised on the initialization thread.");

        viewModel.ApplyPendingUpdates();
        Assert.IsFalse(searchTextChanged, "Publishing the pending update must only enqueue work on the UI scheduler.");

        scheduler.RunAll();
        Assert.IsTrue(searchTextChanged);
    }

    [TestMethod]
    public void IconUpdate_InitializesReplacementIcon()
    {
        var page = new Page
        {
            Id = "page",
            Name = "Page",
            Icon = new IconInfo("initial"),
        };
        var viewModel = new PageViewModel(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty);
        viewModel.InitializeProperties();
        var initialIcon = viewModel.Icon;

        page.Icon = new IconInfo(new IconData("light"), new IconData("dark"));

        Assert.AreNotSame(initialIcon, viewModel.Icon);
        Assert.AreEqual("light", viewModel.Icon.Light.Icon);
        Assert.AreEqual("dark", viewModel.Icon.Dark.Icon);
    }
}
