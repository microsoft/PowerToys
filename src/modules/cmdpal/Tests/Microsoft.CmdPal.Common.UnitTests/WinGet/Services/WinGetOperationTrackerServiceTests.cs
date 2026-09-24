// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;
using Microsoft.CmdPal.Common.WinGet.Services;

namespace Microsoft.CmdPal.Common.UnitTests.WinGet.Services;

[TestClass]
public class WinGetOperationTrackerServiceTests
{
    [TestMethod]
    public void StartOperation_AddsOperationAndRaisesStartedEvent()
    {
        var service = new WinGetOperationTrackerService();
        WinGetPackageOperation? raisedOperation = null;
        service.OperationStarted += (_, e) => raisedOperation = e.Operation;

        var operation = service.StartOperation("Microsoft.PowerToys", "PowerToys", WinGetPackageOperationKind.Install);

        Assert.AreEqual(operation, raisedOperation);
        Assert.AreEqual(1, service.Operations.Count);
        Assert.AreEqual(operation, service.Operations[0]);
        Assert.AreEqual(WinGetPackageOperationState.Queued, operation.State);
        Assert.AreEqual(WinGetPackageOperationSource.Unspecified, operation.Source);
        Assert.IsTrue(operation.IsIndeterminate);
    }

    [TestMethod]
    [DataRow(WinGetPackageOperationSource.Unspecified)]
    [DataRow(WinGetPackageOperationSource.WinGetExtension)]
    public void OperationSourceIsPreservedThroughoutTracking(WinGetPackageOperationSource source)
    {
        var service = new WinGetOperationTrackerService();
        var events = new List<WinGetPackageOperation>();
        service.OperationStarted += (_, e) => events.Add(e.Operation);
        service.OperationUpdated += (_, e) => events.Add(e.Operation);
        service.OperationCompleted += (_, e) => events.Add(e.Operation);

        var operation = service.StartOperation("Microsoft.PowerToys", "PowerToys", WinGetPackageOperationKind.Install, source);
        service.RegisterCancellationHandler(operation.OperationId, () => { });
        Assert.IsTrue(service.TryCancelOperation(operation.OperationId));
        service.UpdateOperation(operation.OperationId, WinGetPackageOperationState.Installing, isIndeterminate: true);
        service.CompleteOperation(operation.OperationId, WinGetPackageOperationState.Succeeded);

        Assert.HasCount(5, events);
        foreach (var snapshot in events)
        {
            Assert.AreEqual(source, snapshot.Source);
        }

        Assert.AreEqual(source, service.Operations[0].Source);
    }

    [TestMethod]
    public void UpdateOperation_UpdatesSnapshotAndRaisesUpdatedEvent()
    {
        var service = new WinGetOperationTrackerService();
        var operation = service.StartOperation("Microsoft.PowerToys", "PowerToys", WinGetPackageOperationKind.Install);
        WinGetPackageOperation? raisedOperation = null;
        service.OperationUpdated += (_, e) => raisedOperation = e.Operation;

        var updated = service.UpdateOperation(
            operation.OperationId,
            WinGetPackageOperationState.Downloading,
            isIndeterminate: false,
            progressPercent: 42,
            bytesDownloaded: 420,
            bytesRequired: 1000);

        Assert.IsNotNull(updated);
        Assert.AreEqual(updated, raisedOperation);
        Assert.AreEqual(WinGetPackageOperationState.Downloading, updated.State);
        Assert.AreEqual(42u, updated.ProgressPercent);
        Assert.AreEqual(420UL, updated.BytesDownloaded);
        Assert.AreEqual(1000UL, updated.BytesRequired);
        Assert.IsFalse(updated.IsIndeterminate);
    }

    [TestMethod]
    public void CompleteOperation_MarksOperationCompletedAndRaisesCompletedEvent()
    {
        var service = new WinGetOperationTrackerService();
        var operation = service.StartOperation("Microsoft.PowerToys", "PowerToys", WinGetPackageOperationKind.Install);
        WinGetPackageOperation? raisedOperation = null;
        service.OperationCompleted += (_, e) => raisedOperation = e.Operation;

        var completed = service.CompleteOperation(operation.OperationId, WinGetPackageOperationState.Failed, "No catalog");

        Assert.IsNotNull(completed);
        Assert.AreEqual(completed, raisedOperation);
        Assert.AreEqual(WinGetPackageOperationState.Failed, completed.State);
        Assert.AreEqual("No catalog", completed.ErrorMessage);
        Assert.IsTrue(completed.IsCompleted);
        Assert.IsNotNull(completed.CompletedAt);
    }

    [TestMethod]
    public void GetLatestOperation_IsCaseInsensitive()
    {
        var service = new WinGetOperationTrackerService();
        var operation = service.StartOperation("Microsoft.PowerToys", "PowerToys", WinGetPackageOperationKind.Install);

        var latest = service.GetLatestOperation("microsoft.powertoys");

        Assert.AreEqual(operation, latest);
    }

    [TestMethod]
    public void TryCancelOperation_InvokesRegisteredCallbackAndClearsCancelableState()
    {
        var service = new WinGetOperationTrackerService();
        var operation = service.StartOperation("Microsoft.PowerToys", "PowerToys", WinGetPackageOperationKind.Install);
        var cancelInvoked = false;

        service.RegisterCancellationHandler(operation.OperationId, () => cancelInvoked = true);

        var registeredOperation = service.GetLatestOperation(operation.PackageId);
        Assert.IsNotNull(registeredOperation);
        Assert.IsTrue(registeredOperation!.CanCancel);

        var wasCanceled = service.TryCancelOperation(operation.OperationId);

        Assert.IsTrue(wasCanceled);
        Assert.IsTrue(cancelInvoked);

        var latest = service.GetLatestOperation(operation.PackageId);
        Assert.IsNotNull(latest);
        Assert.IsFalse(latest!.CanCancel);
    }
}
