// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "../ProtectedStorage.Common/Common.h"
#include "../ProtectedStorage.Common/SignatureTrust.h"
#include <iostream>

using namespace PowerToys::ProtectedStorage;

int wmain(int argc, wchar_t** argv)
{
    try
    {
        if (argc == 3 && std::wstring_view(argv[1]) == L"verify-file")
        {
            const std::wstring path = argv[2];
            Require(IsAbsoluteLocal(path), "Expected an absolute local file path");
            auto file = OpenRead(path, MAXLONGLONG);
            VerifyAuthenticodeReleaseSignature(file.get(), path);
        }
        else if (argc == 4 && std::wstring_view(argv[1]) == L"verify-detached")
        {
            const std::wstring document = argv[2], signature = argv[3];
            Require(IsAbsoluteLocal(document) && IsAbsoluteLocal(signature), "Expected absolute local document and signature paths");
            auto contentFile = OpenRead(document, 65536);
            auto signatureFile = OpenRead(signature, 1048576);
            const auto content = ReadAll(contentFile.get(), 65536);
            const auto encoded = ReadAll(signatureFile.get(), 1048576);
            Require(!content.empty() && !encoded.empty(), "Empty release document or signature");
            VerifyDetachedReleaseSignature(content, encoded);
        }
        else
        {
            std::cerr << "Usage: ProtectedStorage.TrustVerifier.exe verify-file <absolute path>\n"
                         "       ProtectedStorage.TrustVerifier.exe verify-detached <absolute document> <absolute p7s>\n";
            return 2;
        }
        std::cout << "Verified " << ReleaseTrustPolicy << "\n";
        return 0;
    }
    catch (const Error& error)
    {
        std::cerr << error.what() << " (" << error.code << ")\n";
        return 1;
    }
    catch (const std::exception& error)
    {
        std::cerr << error.what() << "\n";
        return 1;
    }
}
