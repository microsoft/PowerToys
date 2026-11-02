// Copyright (c) Microsoft Corporation
// Licensed under the MIT license. See LICENSE in the project root.

#pragma once

NTSTATUS FixtureInitialize(WDFDEVICE device);
NTSTATUS FixtureAttach(IDDCX_MONITOR monitor, UINT connector);
using FixtureArrivalCallback = NTSTATUS (*)(IDDCX_ADAPTER, UINT);
void FixtureAdapterReady(IDDCX_ADAPTER adapter, FixtureArrivalCallback arrive);
void FixtureMonitorArrived(IDDCX_MONITOR monitor, UINT connector);
EVT_IDD_CX_MONITOR_I2C_TRANSMIT FixtureTransmit;
EVT_IDD_CX_MONITOR_I2C_RECEIVE FixtureReceive;
EVT_IDD_CX_DEVICE_IO_CONTROL FixtureIoControl;
