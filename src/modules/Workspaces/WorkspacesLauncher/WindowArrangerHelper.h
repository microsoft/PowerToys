#pragma once

class WindowArrangerHelper
{
public:
    WindowArrangerHelper() = default;
    ~WindowArrangerHelper();

    void Launch(const std::wstring& projectId, bool elevated);

private:
    DWORD uiProcessId;
};
