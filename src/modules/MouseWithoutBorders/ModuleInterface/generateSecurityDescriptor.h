#pragma once
#include <string>

// Isolate this function for the magic strings it has.
inline std::wstring generateSecurityDescriptor(std::wstring Sid) {
    std::wstring securityDescriptor = L"D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)(A;;CR;;;AU)(A;;CCLCSWRPWPDTLOCRRC;;;PU)(A;;RPWPDTLO;;;";
    securityDescriptor += Sid;
    securityDescriptor += L")S:(AU;FA;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;WD)";
    return securityDescriptor;
}
