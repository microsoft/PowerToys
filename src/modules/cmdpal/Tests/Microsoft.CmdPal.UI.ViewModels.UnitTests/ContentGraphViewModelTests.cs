// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ContentGraphViewModelTests
{
    private static readonly double[] ExpectedMeterContributions = [16, 8];
    private static readonly GraphValueScale[] DisplayScales = [new() { Divisor = 1, Suffix = " items" }, new() { Divisor = 1000, Suffix = " k items" }];

    [TestMethod]
    public async Task Initialization_ReturnsWhileTheFirstSnapshotIsBlocked()
    {
        using var graph = new ControlledLineGraph();
        graph.BlockNextRead();
        var context = new TestContext();
        var vm = Create(graph, context);
        var initialization = Task.Run(vm.InitializeProperties);
        try
        {
            Assert.IsTrue(graph.ReadEntered.Wait(TimeSpan.FromSeconds(5)));
            WaitFor(() => initialization.IsCompleted);
            Assert.IsNull(vm.Data);
        }
        finally
        {
            graph.ReleaseRead.Set();
        }

        await initialization;
        WaitFor(() => context.SchedulerImpl.Count > 0);
        context.SchedulerImpl.RunAll();
        Assert.IsNotNull(vm.Data);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    [DataRow(GraphLineStyle.Solid)]
    [DataRow(GraphLineStyle.Dashed)]
    [DataRow(GraphLineStyle.Dotted)]
    public async Task Attachment_SubscribesBeforeReading_AndCachesConfiguration(GraphLineStyle lineStyle)
    {
        using var graph = new ControlledLineGraph { OnSubscribe = graph => graph.Value = 7, LineStyle = lineStyle, AutoScaleMaximum = true, ValueScales = DisplayScales, IsReadoutOnly = true, ReadoutValueSuffix = " GB" };
        var context = new TestContext();
        var vm = Create(graph, context);
        await InitializeGraph(vm, context);
        context.SchedulerImpl.RunAll();
        Assert.AreEqual(7d, vm.Data!.Samples[0].Value);
        Assert.AreEqual(1, graph.SeriesReads);

        Assert.AreEqual("Test graph", vm.Data.Configuration.DisplayName);
        Assert.AreEqual(lineStyle, vm.Data.Configuration.Series[0].LineStyle);
        Assert.IsTrue(vm.Data.Configuration.Series[0].IsReadoutOnly);
        Assert.AreEqual(" GB", vm.Data.Configuration.Series[0].ReadoutValueSuffix);
        Assert.AreEqual(0.75, vm.Data.Configuration.Smoothing);
        Assert.AreEqual(1, graph.SmoothingReads);
        Assert.IsTrue(vm.Data.Configuration.AutoScaleMaximum);
        CollectionAssert.AreEqual(DisplayScales, vm.Data.Configuration.ValueScales);
        Assert.AreEqual(1, graph.ScaleReads);

        graph.Publish(11);
        WaitFor(() => graph.SnapshotReads == 2 && context.SchedulerImpl.Count > 0);
        context.SchedulerImpl.RunAll();
        Assert.AreEqual(11d, vm.Data!.Samples[0].Value);
        Assert.AreEqual(1, graph.SeriesReads);
        Assert.AreEqual(0, graph.PropertyNameReads);
        Assert.AreEqual(lineStyle, vm.Data.Configuration.Series[0].LineStyle);
        Assert.IsTrue(vm.Data.Configuration.Series[0].IsReadoutOnly);
        Assert.AreEqual(" GB", vm.Data.Configuration.Series[0].ReadoutValueSuffix);
        Assert.AreEqual("Test graph", vm.Data.Configuration.DisplayName);
        Assert.AreEqual(0.75, vm.Data.Configuration.Smoothing);
        Assert.AreEqual(1, graph.SmoothingReads);
        Assert.IsTrue(vm.Data.Configuration.AutoScaleMaximum);
        CollectionAssert.AreEqual(DisplayScales, vm.Data.Configuration.ValueScales);
        Assert.AreEqual(1, graph.ScaleReads);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    [DataRow(-0.01d)]
    [DataRow(1.01d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void InvalidSmoothing_RejectsConfigurationBeforeReadingSnapshot(double smoothing)
    {
        using var graph = new ControlledLineGraph { SmoothingAmount = smoothing };
        var context = new TestContext();
        var vm = Create(graph, context);
        vm.InitializeProperties();
        WaitFor(() => context.Errors.Count == 1);
        context.SchedulerImpl.RunAll();
        Assert.IsNull(vm.Data);
        Assert.AreEqual(0, graph.SnapshotReads);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
    }

    [TestMethod]
    public void InvalidLineStyle_RejectsConfigurationBeforeReadingSnapshot()
    {
        using var graph = new ControlledLineGraph { LineStyle = (GraphLineStyle)int.MaxValue };
        var context = new TestContext();
        var vm = Create(graph, context);
        vm.InitializeProperties();
        WaitFor(() => context.Errors.Count == 1);
        context.SchedulerImpl.RunAll();
        Assert.IsNull(vm.Data);
        Assert.AreEqual(0, graph.SnapshotReads);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
    }

    [TestMethod]
    [DataRow(0d, " items")]
    [DataRow(-1d, " items")]
    [DataRow(1d, " items")]
    [DataRow(double.NaN, " items")]
    [DataRow(double.PositiveInfinity, " items")]
    [DataRow(double.NegativeInfinity, " items")]
    [DataRow(1000d, null)]
    public void InvalidValueScales_RejectConfigurationBeforeReadingSnapshot(double divisor, string? suffix)
    {
        using var graph = new ControlledLineGraph { ValueScales = [DisplayScales[0], new GraphValueScale { Divisor = divisor, Suffix = suffix! }] };
        var context = new TestContext();
        var vm = Create(graph, context);
        vm.InitializeProperties();
        WaitFor(() => context.Errors.Count == 1);
        context.SchedulerImpl.RunAll();
        Assert.IsNull(vm.Data);
        Assert.AreEqual(0, graph.SnapshotReads);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
    }

    [TestMethod]
    public async Task BurstDuringRead_CoalescesRemoteReadsAndPendingUiUpdates()
    {
        using var graph = new ControlledLineGraph();
        var context = new TestContext();
        var vm = Create(graph, context);
        await InitializeGraph(vm, context);
        graph.BlockNextRead();
        graph.Publish(1);
        Assert.IsTrue(graph.ReadEntered.Wait(TimeSpan.FromSeconds(5)));
        for (var value = 2; value <= 100; value++)
        {
            graph.Publish(value);
        }

        graph.ReleaseRead.Set();
        WaitFor(() => graph.SnapshotReads == 3 && graph.ActiveReads == 0);
        Assert.AreEqual(1, context.SchedulerImpl.Count);
        WaitFor(() =>
        {
            context.SchedulerImpl.RunAll();
            return vm.Data?.Samples[0].Value == 100;
        });
        Assert.AreEqual(100d, vm.Data!.Samples[0].Value);
        Assert.AreEqual(1, graph.MaximumConcurrentReads);
        Assert.AreEqual(1, graph.SeriesReads);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    public async Task Cleanup_DiscardsAnInflightRead_AndUnsubscribes()
    {
        using var graph = new ControlledLineGraph();
        var context = new TestContext();
        var vm = Create(graph, context);
        await InitializeGraph(vm, context);
        context.SchedulerImpl.RunAll();
        var initial = vm.Data;

        graph.BlockNextRead();
        graph.Publish(1);
        Assert.IsTrue(graph.ReadEntered.Wait(TimeSpan.FromSeconds(5)));
        vm.SafeCleanup();
        graph.ReleaseRead.Set();
        WaitFor(() => graph.Subscribers == 0);
        context.SchedulerImpl.RunAll();
        Assert.AreSame(initial, vm.Data);
        var reads = graph.SnapshotReads;
        graph.Publish(2);
        Assert.AreEqual(reads, graph.SnapshotReads);
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    public async Task InvalidSnapshot_PreservesPreviousData_AndLaterValidDataRecovers()
    {
        using var graph = new ControlledLineGraph();
        var context = new TestContext();
        var vm = Create(graph, context);
        await InitializeGraph(vm, context);
        context.SchedulerImpl.RunAll();
        var initial = vm.Data;

        graph.Publish(double.NaN);
        WaitFor(() => context.Errors.Count == 1);
        context.SchedulerImpl.RunAll();
        Assert.AreSame(initial, vm.Data);
        graph.Publish(42);
        WaitFor(() => context.SchedulerImpl.Count > 0);
        context.SchedulerImpl.RunAll();
        Assert.AreEqual(42d, vm.Data!.Samples[0].Value);
        vm.SafeCleanup();
        WaitFor(() => graph.Subscribers == 0);
    }

    [TestMethod]
    public async Task EmptyHistory_ClearsData_AndOutOfRangeMeasurementsArePreserved()
    {
        var graph = new LineGraphContent([new GraphSeriesInfo { Name = "Same" }, new GraphSeriesInfo { Name = "Same" }]);
        var time = DateTimeOffset.UtcNow;
        graph.SetSnapshot(
        [
            new GraphSample { SeriesIndex = 1, Timestamp = time.AddSeconds(-2), Value = 123 },
            new GraphSample { SeriesIndex = 0, Timestamp = time.AddSeconds(-1), Value = -5 },
        ]);
        var context = new TestContext();
        var vm = Create(graph, context);
        await InitializeGraph(vm, context);
        context.SchedulerImpl.RunAll();
        Assert.AreEqual(123d, vm.Data!.Samples[0].Value);
        Assert.AreEqual(-5d, vm.Data.Samples[1].Value);
        graph.SetSnapshot([]);
        WaitFor(() => context.SchedulerImpl.Count > 0);
        context.SchedulerImpl.RunAll();
        Assert.IsEmpty(vm.Data!.Samples);
        vm.SafeCleanup();
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    public async Task MeterAndDoughnut_PreserveRawValuesAndCoherentText()
    {
        var series = new[] { new GraphSeriesInfo { Name = "A" }, new GraphSeriesInfo { Name = "B" } };
        var meter = new VerticalUsageBarContent(series, minimum: 10, maximum: 50);
        meter.SetSnapshot(34, "24 used", [16, 8]);
        var doughnut = new DoughnutGraphContent(series);
        doughnut.SetSnapshot([double.MaxValue, double.MaxValue], "2 units", "Total");
        var context = new TestContext();
        var meterVm = Create(meter, context);
        var doughnutVm = Create(doughnut, context);
        await Task.Run(meterVm.InitializeProperties);
        await Task.Run(doughnutVm.InitializeProperties);
        WaitFor(() =>
        {
            context.SchedulerImpl.RunAll();
            return meterVm.Data is not null && doughnutVm.Data is not null;
        });
        Assert.AreEqual(34d, meterVm.Data!.Value);
        Assert.AreEqual("24 used", meterVm.Data.ValueText);
        CollectionAssert.AreEqual(ExpectedMeterContributions, meterVm.Data.Values);
        CollectionAssert.AreEqual(new[] { double.MaxValue, double.MaxValue }, doughnutVm.Data!.Values);
        Assert.AreEqual("2 units", doughnutVm.Data.ValueText);
        Assert.AreEqual("Total", doughnutVm.Data.CenterLabel);
        meterVm.SafeCleanup();
        doughnutVm.SafeCleanup();
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    public void ContentFactory_RecognizesAllGraphTypes_IncludingTreeChildren()
    {
        var context = new TestContext();
        var weakContext = new WeakReference<IPageContext>(context);
        var tree = new ContentTreeViewModel(new TreeContent(), weakContext);
        IContent[] contents = [new LineGraphContent([]), new VerticalUsageBarContent(), new DoughnutGraphContent([])];
        foreach (var content in contents)
        {
            Assert.IsInstanceOfType<ContentGraphViewModel>(CommandPaletteContentPageViewModel.CreateViewModel(content, weakContext));
            Assert.IsInstanceOfType<ContentGraphViewModel>(tree.ViewModelFromContent(content, weakContext));
        }
    }

    [TestMethod]
    public async Task DetailsReplacement_CleansOldGraphs_AndCleanupCleansTheReplacement()
    {
        using var first = new ControlledLineGraph();
        using var second = new ControlledLineGraph();
        var details = new Details { Content = [first] };
        var context = new TestContext();
        var vm = new DetailsViewModel(details, new WeakReference<IPageContext>(context));
        await Task.Run(vm.InitializeProperties);
        context.SchedulerImpl.RunAll();
        WaitFor(() => first.Subscribers == 1);

        await Task.Run(() => details.Content = [second]);
        context.SchedulerImpl.RunAll();
        WaitFor(() => first.Subscribers == 0);
        WaitFor(() => second.Subscribers == 1);
        Assert.IsInstanceOfType<ContentGraphViewModel>(vm.Content.Single());
        var changedOnUiScheduler = false;
        vm.Content.CollectionChanged += (_, _) => changedOnUiScheduler = TaskScheduler.Current == context.SchedulerImpl;
        await Task.Run(vm.SafeCleanup);
        Assert.HasCount(1, vm.Content);
        context.SchedulerImpl.RunAll();
        WaitFor(() => second.Subscribers == 0);
        Assert.IsEmpty(vm.Content);
        Assert.IsTrue(changedOnUiScheduler);
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    public async Task DetailsCleanup_DiscardsContentStillQueuedForTheUi()
    {
        using var graph = new ControlledLineGraph();
        var details = new Details { Content = [graph] };
        var context = new TestContext();
        var vm = new DetailsViewModel(details, new WeakReference<IPageContext>(context));
        await Task.Run(vm.InitializeProperties);
        vm.SafeCleanup();
        context.SchedulerImpl.RunAll();
        WaitFor(() => graph.Subscribers == 0);
        Assert.IsEmpty(vm.Content);
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    [TestMethod]
    public async Task TreeRootReplacement_InitializesNewGraph_AndCleansOldGraph()
    {
        using var first = new ControlledLineGraph();
        using var second = new ControlledLineGraph();
        var tree = new TreeContent { RootContent = first };
        var context = new TestContext();
        var vm = new ContentTreeViewModel(tree, new WeakReference<IPageContext>(context));
        await Task.Run(vm.InitializeProperties);
        context.SchedulerImpl.RunAll();
        await Task.Run(() => tree.RootContent = second);
        context.SchedulerImpl.RunAll();
        WaitFor(() => first.Subscribers == 0);
        WaitFor(() =>
        {
            context.SchedulerImpl.RunAll();
            return second.Subscribers == 1 && ((ContentGraphViewModel)vm.RootContent!).Data is not null;
        });
        await Task.Run(() => tree.RootContent = null);
        context.SchedulerImpl.RunAll();
        WaitFor(() => second.Subscribers == 0);
        Assert.IsNull(vm.RootContent);
        vm.SafeCleanup();
        Assert.IsTrue(context.Errors.IsEmpty);
    }

    private static ContentGraphViewModel Create(IContent graph, TestContext context)
        => new(graph, new WeakReference<IPageContext>(context));

    private static async Task InitializeGraph(ContentGraphViewModel vm, TestContext context)
    {
        await Task.Run(vm.InitializeProperties);
        WaitFor(() => context.SchedulerImpl.Count > 0);
    }

    private static void WaitFor(Func<bool> condition)
        => Assert.IsTrue(SpinWait.SpinUntil(condition, TimeSpan.FromSeconds(5)), "Timed out waiting for the graph worker.");

    private sealed class TestContext : IPageContext
    {
        public QueuedScheduler SchedulerImpl { get; } = new();

        public ConcurrentQueue<Exception> Errors { get; } = new();

        public TaskScheduler Scheduler => SchedulerImpl;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) => Errors.Enqueue(ex);
    }

    private sealed class QueuedScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = new();

        public int Count => _tasks.Count;

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void RunAll()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
                task.GetAwaiter().GetResult();
            }
        }
    }

    private sealed partial class ControlledLineGraph : ILineGraphContent, IDisposable
    {
        private TypedEventHandler<object, IPropChangedEventArgs>? _handlers;
        private int _blockRead;
        private int _activeReads;
        private int _snapshotReads;
        private int _subscribers;
        private int _propertyNameReads;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged
        {
            add
            {
                _handlers += value;
                Interlocked.Increment(ref _subscribers);
                OnSubscribe?.Invoke(this);
            }

            remove
            {
                _handlers -= value;
                Interlocked.Decrement(ref _subscribers);
            }
        }

        public Action<ControlledLineGraph>? OnSubscribe { get; init; }

        public ManualResetEventSlim ReadEntered { get; } = new();

        public ManualResetEventSlim ReleaseRead { get; } = new(true);

        public int SeriesReads { get; private set; }

        public int SmoothingReads { get; private set; }

        public double SmoothingAmount { get; init; } = 0.75;

        public GraphLineStyle LineStyle { get; init; }

        public bool IsReadoutOnly { get; init; }

        public string ReadoutValueSuffix { get; init; } = string.Empty;

        public int SnapshotReads => Volatile.Read(ref _snapshotReads);

        public int ActiveReads => Volatile.Read(ref _activeReads);

        public int Subscribers => Volatile.Read(ref _subscribers);

        public int PropertyNameReads => Volatile.Read(ref _propertyNameReads);

        public int MaximumConcurrentReads { get; private set; }

        public double Value { get; set; }

        public string DisplayName => "Test graph";

        public double Minimum => 0;

        public double Maximum => 100;

        public TimeSpan HistoryDuration => TimeSpan.FromSeconds(60);

        public string ValueFormat => "0.0";

        public string ValueSuffix => "%";

        public bool AutoScaleMaximum { get; init; }

        public GraphValueScale[] ValueScales { get; init; } = [];

        public int ScaleReads { get; private set; }

        public double Smoothing
        {
            get
            {
                SmoothingReads++;
                return SmoothingAmount;
            }
        }

        public GraphValueScale[] GetValueScales()
        {
            ScaleReads++;
            return [.. ValueScales];
        }

        public GraphSeriesInfo[] GetSeries()
        {
            SeriesReads++;
            return [new GraphSeriesInfo { Name = "Test", LineStyle = LineStyle, IsReadoutOnly = IsReadoutOnly, ReadoutValueSuffix = ReadoutValueSuffix }];
        }

        public GraphSample[] GetSnapshot()
        {
            var concurrent = Interlocked.Increment(ref _activeReads);
            MaximumConcurrentReads = Math.Max(MaximumConcurrentReads, concurrent);
            Interlocked.Increment(ref _snapshotReads);
            var value = Value;
            try
            {
                if (Interlocked.Exchange(ref _blockRead, 0) == 1)
                {
                    ReadEntered.Set();
                    if (!ReleaseRead.Wait(TimeSpan.FromSeconds(5)))
                    {
                        throw new TimeoutException("Blocked graph read was not released.");
                    }
                }

                return [new GraphSample { Timestamp = DateTimeOffset.UnixEpoch, Value = value }];
            }
            finally
            {
                Interlocked.Decrement(ref _activeReads);
            }
        }

        public void BlockNextRead()
        {
            ReadEntered.Reset();
            ReleaseRead.Reset();
            Interlocked.Exchange(ref _blockRead, 1);
        }

        public void Publish(double value)
        {
            Value = value;
            _handlers?.Invoke(this, new UnreadableEventArgs(this));
        }

        public void Dispose()
        {
            ReleaseRead.Set();
            ReadEntered.Dispose();
            ReleaseRead.Dispose();
        }

        private sealed partial class UnreadableEventArgs(ControlledLineGraph owner) : IPropChangedEventArgs
        {
            public string PropertyName
            {
                get
                {
                    Interlocked.Increment(ref owner._propertyNameReads);
                    throw new InvalidOperationException("The callback must not read remote event arguments.");
                }
            }
        }
    }
}
