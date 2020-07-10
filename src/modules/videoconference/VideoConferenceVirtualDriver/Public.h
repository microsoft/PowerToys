/*++

Module Name:

    public.h

Abstract:

    This module contains the common declarations shared by driver
    and user applications.

Environment:

    driver and application

--*/

//
// Define an Interface Guid so that apps can find the device and talk to it.
//

DEFINE_GUID (GUID_DEVINTERFACE_SimpleMediaSourceDriver,
    0xb5036295,0xf041,0x4506,0x88,0xdc,0xcb,0x16,0x5c,0x4d,0x67,0x8c);
// {b5036295-f041-4506-88dc-cb165c4d678c}
