#include "../Common/ProtoCommon.h"

#include <iostream>

namespace
{
    std::wstring make_full_name(uint16_t major)
    {
        PACKAGE_ID id{};
        id.processorArchitecture = PROCESSOR_ARCHITECTURE_AMD64;
        id.version.Major = major;
        std::wstring name(ptap::PackageName);
        std::wstring publisher(ptap::PackagePublisher);
        id.name = name.data();
        id.publisher = publisher.data();
        UINT32 chars = 0;
        LONG result = PackageFullNameFromId(&id, &chars, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptap::win32_error("PackageFullNameFromId(size)", result);
        }
        std::wstring fullName(chars, L'\0');
        result = PackageFullNameFromId(&id, &chars, fullName.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptap::win32_error("PackageFullNameFromId", result);
        }
        fullName.resize(chars - 1);
        return fullName;
    }

    void require(bool condition, const char* message)
    {
        if (!condition)
        {
            throw std::runtime_error(message);
        }
    }
}

int wmain()
{
    try
    {
        const auto version1 = ptap::validate_package_full_name(make_full_name(1));
        const auto version2 = ptap::validate_package_full_name(make_full_name(2));
        const auto version3 = ptap::validate_package_full_name(make_full_name(3));
        require(version1.version.major == 1, "v1 policy");
        require(version2.version.major == 2, "v2 policy");
        require(version3.version.major == 3, "v3 fail-safe probe policy");
        require(ptap::version_value(version2.version) > ptap::version_value(version1.version), "monotonic version");
        require(version1.familyName == version2.familyName, "version-agnostic family");
        require(version1.familyName == ptap::expected_package_family_name(), "fixed family");

        bool rejected = false;
        try
        {
            const auto spoof =
                ptap::validate_package_full_name(L"Contoso.Spoof_1.0.0.0_x64__aaaaaaaaaaaaa");
            (void)spoof;
        }
        catch (const ptap::win32_error&)
        {
            rejected = true;
        }
        require(rejected, "spoof package rejection");

        wchar_t bounded[16]{};
        ptap::copy_bounded(bounded, ARRAYSIZE(bounded), L"prototype");
        require(ptap::bounded_string(bounded, ARRAYSIZE(bounded)) == L"prototype", "bounded string");
        require(ptap::quote_argument(L"C:\\Program Files\\Pt Alias\\") == L"\"C:\\Program Files\\Pt Alias\\\\\"", "argument quoting");

        const auto currentSid = ptap::current_token_user_sid();
        const auto first = ptap::instance_names(currentSid);
        const auto second = ptap::instance_names(currentSid);
        require(first.suffix.size() == 8, "instance suffix length");
        require(first.serviceName == second.serviceName, "deterministic service name");
        require(first.accountName.size() <= 20, "local account name bound");
        require(
            ptap::service_sid(L"TrustedInstaller") ==
                ptap::sid_for_account(L"NT SERVICE\\TrustedInstaller"),
            "service SID derivation");
        require(sizeof(ptap::RequestHeader) == 16, "request protocol layout");
        require(sizeof(ptap::ReplyHeader) == 20, "reply protocol layout");
        require(ptap::MaxProtocolPayload <= 1024, "protocol payload bound");

        std::wcout << L"PASS: identity policy, monotonic versions, fixed family, bounded protocol, quoting, deterministic names\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "FAIL: " << error.what() << "\n";
        return 1;
    }
}
