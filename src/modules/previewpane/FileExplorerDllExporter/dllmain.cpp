// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"
#include "ClassFactory.h"
#include <common/interop/shared_constants.h>
#include <filesystem>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/logger/logger_settings.h>

HINSTANCE g_hInst = NULL;
long g_cDllRef = 0;

// {0e6d5bdd-d5f8-4692-a089-8bb88cdd37f4}
static const GUID CLSID_BgcodePreviewHandler = { 0x0e6d5bdd, 0xd5f8, 0x4692, { 0xa0, 0x89, 0x8b, 0xb8, 0x8c, 0xdd, 0x37, 0xf4 } };

// {A0257634-8812-4CE8-AF11-FA69ACAEAFAE}
static const GUID CLSID_GcodePreviewHandler = { 0xa0257634, 0x8812, 0x4ce8, { 0xaf, 0x11, 0xfa, 0x69, 0xac, 0xae, 0xaf, 0xae } };

// {60789D87-9C3C-44AF-B18C-3DE2C2820ED3}
static const GUID CLSID_MarkdownPreviewHandler = { 0x60789d87, 0x9c3c, 0x44af, { 0xb1, 0x8c, 0x3d, 0xe2, 0xc2, 0x82, 0xe, 0xd3 } };

// {D8034CFA-F34B-41FE-AD45-62FCBB52A6DA}
static const GUID CLSID_MonacoPreviewHandler = { 0xd8034cfa, 0xf34b, 0x41fe, { 0xad, 0x45, 0x62, 0xfc, 0xbb, 0x52, 0xa6, 0xda } };

// {A5A41CC7-02CB-41D4-8C9B-9087040D6098}
static const GUID CLSID_PdfPreviewHandler = { 0xa5a41cc7, 0x2cb, 0x41d4, { 0x8c, 0x9b, 0x90, 0x87, 0x4, 0xd, 0x60, 0x98 } };

// {729B72CD-B72E-4FE9-BCBF-E954B33FE699}
static const GUID CLSID_QoiPreviewHandler = { 0x729b72cd, 0xb72e, 0x4fe9, { 0xbc, 0xbf, 0xe9, 0x54, 0xb3, 0x3f, 0xe6, 0x99 } };

// {FCDD4EED-41AA-492F-8A84-31A1546226E0}
static const GUID CLSID_SvgPreviewHandler = { 0xfcdd4eed, 0x41aa, 0x492f, { 0x8a, 0x84, 0x31, 0xa1, 0x54, 0x62, 0x26, 0xe0 } };

// {5c93a1e4-99d0-4fb3-991c-6c296a27be21}
static const GUID CLSID_BgcodeThumbnailProvider = { 0x5c93a1e4, 0x99d0, 0x4fb3, { 0x99, 0x1c, 0x6c, 0x29, 0x6a, 0x27, 0xbe, 0x21 } };

// {F2847CBE-CD03-4C83-A359-1A8052C1B9D5}
static const GUID CLSID_GcodeThumbnailProvider = { 0xf2847cbe, 0xcd03, 0x4c83, { 0xa3, 0x59, 0x1a, 0x80, 0x52, 0xc1, 0xb9, 0xd5 } };

// {D8BB9942-93BD-412D-87E4-33FAB214DC1A}
static const GUID CLSID_PdfThumbnailProvider = { 0xd8bb9942, 0x93bd, 0x412d, { 0x87, 0xe4, 0x33, 0xfa, 0xb2, 0x14, 0xdc, 0x1a } };

// {AD856B15-D25E-4008-AFB7-AFAA55586188}
static const GUID CLSID_QoiThumbnailProvider = { 0xad856b15, 0xd25e, 0x4008, { 0xaf, 0xb7, 0xaf, 0xaa, 0x55, 0x58, 0x61, 0x88 } };

// {77257004-6F25-4521-B602-50ECC6EC62A6}
static const GUID CLSID_StlThumbnailProvider = { 0x77257004, 0x6f25, 0x4521, { 0xb6, 0x2, 0x50, 0xec, 0xc6, 0xec, 0x62, 0xa6 } };

// {10144713-1526-46C9-88DA-1FB52807A9FF}
static const GUID CLSID_SvgThumbnailProvider = { 0x10144713, 0x1526, 0x46c9, { 0x88, 0xda, 0x1f, 0xb5, 0x28, 0x7, 0xa9, 0xff } };

BOOL APIENTRY DllMain(HMODULE hModule,
                      DWORD ul_reason_for_call,
                      LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

//
//   FUNCTION: DllGetClassObject
//
//   PURPOSE: Create the class factory and query to the specific interface.
//
//   PARAMETERS:
//   * rclsid - The CLSID that will associate the correct data and code.
//   * riid - A reference to the identifier of the interface that the caller
//     is to use to communicate with the class object.
//   * ppv - The address of a pointer variable that receives the interface
//     pointer requested in riid. Upon successful return, *ppv contains the
//     requested interface pointer. If an error occurs, the interface pointer
//     is NULL.
//
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    HRESULT hr = E_OUTOFMEMORY;

    std::filesystem::path logFilePath(PTSettingsHelper::get_local_low_folder_location());

    ClassFactory* pClassFactory = nullptr;

    if (IsEqualCLSID(CLSID_BgcodePreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::bgcodePrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::bgcodePrevLoggerName, logFilePath.wstring(), CommonSharedConstants::BGCODE_PREVIEW_RESIZE_EVENT, L"PowerToys.BgcodePreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_GcodePreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::gcodePrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::gcodePrevLoggerName, logFilePath.wstring(), CommonSharedConstants::GCODE_PREVIEW_RESIZE_EVENT, L"PowerToys.GcodePreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_MarkdownPreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::mdPrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::mdPrevLoggerName, logFilePath.wstring(), CommonSharedConstants::MARKDOWN_PREVIEW_RESIZE_EVENT, L"PowerToys.MarkdownPreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_MonacoPreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::monacoPrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::monacoPrevLoggerName, logFilePath.wstring(), CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT, L"PowerToys.MonacoPreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_PdfPreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::pdfPrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::pdfPrevLoggerName, logFilePath.wstring(), CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT, L"PowerToys.PdfPreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_QoiPreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::qoiPrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::qoiPrevLoggerName, logFilePath.wstring(), CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT, L"PowerToys.QoiPreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_SvgPreviewHandler, rclsid))
    {
        logFilePath.append(LogSettings::svgPrevLogPath);
        pClassFactory = new ClassFactory(LogSettings::svgPrevLoggerName, logFilePath.wstring(), CommonSharedConstants::DEV_FILES_PREVIEW_RESIZE_EVENT, L"PowerToys.SvgPreviewHandler.exe");
    }
    else if (IsEqualCLSID(CLSID_BgcodeThumbnailProvider, rclsid))
    {
        logFilePath.append(LogSettings::bgcodeThumbLogPath);
        pClassFactory = new ClassFactory(LogSettings::bgcodeThumbLoggerName, logFilePath.wstring(), L"PowerToys.BgcodeThumbnailProvider.exe", L"BgcodeThumbnail-Temp", L".bgcode");
    }
    else if (IsEqualCLSID(CLSID_GcodeThumbnailProvider, rclsid))
    {
        logFilePath.append(LogSettings::gcodeThumbLogPath);
        pClassFactory = new ClassFactory(LogSettings::gcodeThumbLoggerName, logFilePath.wstring(), L"PowerToys.GcodeThumbnailProvider.exe", L"GCodeThumbnail-Temp", L".gcode");
    }
    else if (IsEqualCLSID(CLSID_PdfThumbnailProvider, rclsid))
    {
        logFilePath.append(LogSettings::pdfThumbLogPath);
        pClassFactory = new ClassFactory(LogSettings::pdfThumbLoggerName, logFilePath.wstring(), L"PowerToys.PdfThumbnailProvider.exe", L"PdfThumbnail-Temp", L".pdf");
    }
    else if (IsEqualCLSID(CLSID_QoiThumbnailProvider, rclsid))
    {
        logFilePath.append(LogSettings::qoiThumbLogPath);
        pClassFactory = new ClassFactory(LogSettings::qoiThumbLoggerName, logFilePath.wstring(), L"PowerToys.QoiThumbnailProvider.exe", L"QoiThumbnail-Temp", L".qoi");
    }
    else if (IsEqualCLSID(CLSID_StlThumbnailProvider, rclsid))
    {
        logFilePath.append(LogSettings::stlThumbLogPath);
        pClassFactory = new ClassFactory(LogSettings::stlThumbLoggerName, logFilePath.wstring(), L"PowerToys.StlThumbnailProvider.exe", L"StlThumbnail-Temp", L".stl");
    }
    else if (IsEqualCLSID(CLSID_SvgThumbnailProvider, rclsid))
    {
        logFilePath.append(LogSettings::svgThumbLogPath);
        pClassFactory = new ClassFactory(LogSettings::svgThumbLoggerName, logFilePath.wstring(), L"PowerToys.SvgThumbnailProvider.exe", L"SvgThumbnail-Temp", L".svg");
    }
    else
    {
        hr = CLASS_E_CLASSNOTAVAILABLE;
    }

    if (pClassFactory)
    {
        hr = pClassFactory->QueryInterface(riid, ppv);
        pClassFactory->Release();
    }

    return hr;
}

//
//   FUNCTION: DllCanUnloadNow
//
//   PURPOSE: Check if we can unload the component from the memory.
//
//   NOTE: The component can be unloaded from the memory when its reference
//   count is zero (i.e. nobody is still using the component).
//
STDAPI DllCanUnloadNow(void)
{
    return g_cDllRef > 0 ? S_FALSE : S_OK;
}
