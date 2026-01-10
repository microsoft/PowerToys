#pragma once
#include "pch.h"
#include <Unknwn.h>
#include <string>

class ClassFactory : public IClassFactory
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv);
    IFACEMETHODIMP LockServer(BOOL fLock);

    ClassFactory(std::string name, std::wstring logFilePath, const wchar_t* resizeEvent, std::wstring exeName);
    ClassFactory(std::string name, std::wstring logFilePath, std::wstring exeName, std::wstring tempFolderName, std::wstring extension);

protected:
    ~ClassFactory();

private:
    long m_cRef;
    std::string m_name;
    std::wstring m_logFilePath;
    const wchar_t* m_resizeEvent;
    std::wstring m_exeName;
    std::wstring m_tempFolderName;
    std::wstring m_extension;
};
