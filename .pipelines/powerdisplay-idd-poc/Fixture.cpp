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
    DdcEmulator states[2];
}

NTSTATUS FixtureInitialize(WDFDEVICE device)
{
    std::lock_guard lock(gate);
    for (ULONG i = 0; i < 2; ++i)
    {
        states[i] = {};
        states[i].Snapshot.Connector = i;
        states[i].Snapshot.Brightness = 40 + i * 20;
    }
    return WdfDeviceCreateDeviceInterface(device, &FixtureInterface, nullptr);
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
        *snapshot = { 1, { states[0].Snapshot, states[1].Snapshot } };
    }
    WdfRequestCompleteWithInformation(request, STATUS_SUCCESS, sizeof(*snapshot));
}
