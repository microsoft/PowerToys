// Copyright (c) Microsoft Corporation
// Licensed under the MIT license. See LICENSE in the project root.

#include "Driver.h"
#include "Fixture.h"
#include "Protocol.h"
#include "DdcEmulator.h"
#include <mutex>

namespace
{
    struct MonitorContext
    {
        UINT Connector;
    };
    WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(MonitorContext, GetMonitorContext);

    // This prototype creates exactly one adapter per UMDF host.
    std::mutex gate;
    std::mutex controlGate;
    DdcEmulator states[2];
    IDDCX_MONITOR monitors[2]{};
    IDDCX_ADAPTER fixtureAdapter{};
    FixtureArrivalCallback arriveMonitor{};

    NTSTATUS ApplyCommand(const FixtureCommand& command)
    {
        if (command.Version != ProtocolVersion || command.Connector >= 2 ||
            (command.Operation != FixtureOperation::SetConnected && command.Operation != FixtureOperation::SetMode) ||
            (command.Operation == FixtureOperation::SetConnected && command.Value > 1) ||
            (command.Operation == FixtureOperation::SetMode && command.Value > static_cast<ULONG>(FixtureMode::CommunicationFailure)))
        {
            return STATUS_INVALID_PARAMETER;
        }
        std::lock_guard controlLock(controlGate);
        IDDCX_MONITOR monitor{};
        IDDCX_ADAPTER adapter{};
        FixtureArrivalCallback callback{};
        {
            std::lock_guard lock(gate);
            auto& state = states[command.Connector];
            if (command.Operation == FixtureOperation::SetMode)
            {
                state.Snapshot.Mode = static_cast<FixtureMode>(command.Value);
                state.ReplyLength = 0;
                return STATUS_SUCCESS;
            }
            monitor = monitors[command.Connector];
            if ((monitor != nullptr) == (command.Value != 0))
            {
                return STATUS_SUCCESS;
            }
            adapter = fixtureAdapter;
            callback = arriveMonitor;
        }
        if (!adapter || !callback)
        {
            return STATUS_DEVICE_NOT_READY;
        }
        // Arrival/departure can wait for I2C callbacks; never hold their state lock.
        if (command.Value != 0)
        {
            return callback(adapter, command.Connector);
        }
        const auto status = IddCxMonitorDeparture(monitor);
        if (NT_SUCCESS(status))
        {
            std::lock_guard lock(gate);
            monitors[command.Connector] = nullptr;
            states[command.Connector].Snapshot.Connected = 0;
            ++states[command.Connector].Snapshot.Departures;
        }
        return status;
    }
}

NTSTATUS FixtureInitialize(WDFDEVICE device)
{
    std::lock_guard lock(gate);
    for (ULONG i = 0; i < 2; ++i)
    {
        states[i] = {};
        states[i].Snapshot.Connector = i;
        states[i].Snapshot.Brightness = 40 + i * 20;
        monitors[i] = nullptr;
    }
    fixtureAdapter = nullptr;
    arriveMonitor = nullptr;
    return WdfDeviceCreateDeviceInterface(device, &FixtureInterface, nullptr);
}

void FixtureAdapterReady(IDDCX_ADAPTER adapter, FixtureArrivalCallback arrive)
{
    std::lock_guard lock(gate);
    fixtureAdapter = adapter;
    arriveMonitor = arrive;
}

void FixtureMonitorArrived(IDDCX_MONITOR monitor, UINT connector)
{
    std::lock_guard lock(gate);
    monitors[connector] = monitor;
    states[connector].Snapshot.Connected = 1;
    ++states[connector].Snapshot.Arrivals;
}

NTSTATUS FixtureAttach(IDDCX_MONITOR monitor, UINT connector)
{
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, MonitorContext);
    MonitorContext* context = nullptr;
    const auto status = WdfObjectAllocateContext(monitor, &attributes, reinterpret_cast<void**>(&context));
    if (!NT_SUCCESS(status))
    {
        return status;
    }
    context->Connector = connector;
    return STATUS_SUCCESS;
}

_Use_decl_annotations_
    NTSTATUS
    FixtureTransmit(IDDCX_MONITOR monitor, const IDARG_IN_I2C_TRANSMIT* input)
{
    std::lock_guard lock(gate);
    return states[GetMonitorContext(monitor)->Connector].Transmit(input->SevenBitI2CAddress,
                                                                  static_cast<const BYTE*>(input->pData),
                                                                  input->DataSizeInBytes) ?
               STATUS_SUCCESS :
               STATUS_INVALID_PARAMETER;
}

_Use_decl_annotations_
    NTSTATUS
    FixtureReceive(IDDCX_MONITOR monitor, const IDARG_IN_I2C_RECEIVE* input)
{
    std::lock_guard lock(gate);
    return states[GetMonitorContext(monitor)->Connector].Receive(input->SevenBitI2CAddress,
                                                                 static_cast<BYTE*>(input->pData),
                                                                 input->DataSizeInBytes) ?
               STATUS_SUCCESS :
               STATUS_INVALID_PARAMETER;
}

_Use_decl_annotations_ void FixtureIoControl(WDFDEVICE device, WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG code)
{
    UNREFERENCED_PARAMETER(device);
    UNREFERENCED_PARAMETER(outputLength);
    if (code == ControlIoctl && inputLength == sizeof(FixtureCommand))
    {
        FixtureCommand* command = nullptr;
        const auto status = WdfRequestRetrieveInputBuffer(request, sizeof(FixtureCommand), reinterpret_cast<void**>(&command), nullptr);
        WdfRequestComplete(request, NT_SUCCESS(status) ? ApplyCommand(*command) : status);
        return;
    }
    if (code != SnapshotIoctl || inputLength != 0)
    {
        WdfRequestComplete(request, STATUS_INVALID_DEVICE_REQUEST);
        return;
    }
    FixtureSnapshot* snapshot = nullptr;
    const auto status = WdfRequestRetrieveOutputBuffer(request, sizeof(FixtureSnapshot), reinterpret_cast<void**>(&snapshot), nullptr);
    if (!NT_SUCCESS(status))
    {
        WdfRequestComplete(request, status);
        return;
    }
    {
        std::lock_guard lock(gate);
        *snapshot = { ProtocolVersion, { states[0].Snapshot, states[1].Snapshot } };
    }
    WdfRequestCompleteWithInformation(request, STATUS_SUCCESS, sizeof(*snapshot));
}
