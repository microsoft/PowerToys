#pragma once

// Video Conference Mute was a utility we deprecated. However, this required a manual user disable of the module to remove the camera registration, so we include the disable code here to be able to clean up.
void clean_video_conference()
{
    // 31AD75E9-8C3A-49C8-B9ED-5880D6B4A764 is the CLSID GUID for the 64 video conference mute driver.
    // 31AD75E9-8C3A-49C8-B9ED-5880D6B4A732 is the CLSID GUID for the 32 video conference mute driver.
    // 860BB310-5D01-11D0-BD3B-00A0C911CE86 is the CLSID GUID for CLSID_VideoInputDeviceCategory.

    // Unregister the 64 bit driver CLSID:
    RegDeleteTreeW(HKEY_CLASSES_ROOT, L"CLSID\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A764}");
    // Unregister the 64 bit driver CLSID from Video Input Devices:
    RegDeleteTreeW(HKEY_CLASSES_ROOT, L"CLSID\\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\\Instance\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A764}");
    // Unregister the 32 bit driver CLSID:
    RegDeleteTreeW(HKEY_LOCAL_MACHINE, L"Software\\WOW6432Node\\Classes\\CLSID\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A732}");
    // Unregister the 32 bit driver CLSID from Video Input Devices:
    RegDeleteTreeW(HKEY_LOCAL_MACHINE, L"Software\\WOW6432Node\\Classes\\CLSID\\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\\Instance\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A732}");
}
