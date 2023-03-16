#include "pch.h"
#include "Util.h"

static int s_classId = 0;

namespace Mocks
{
    class HwndCreator
    {
    public:
        HwndCreator(const std::wstring& title = L"");

        ~HwndCreator();

        HWND operator()(HINSTANCE hInst);

        void setHwnd(HWND val);
        void setCondition(bool cond);

        inline HINSTANCE getHInstance() const { return m_hInst; }
        inline const std::wstring& getTitle() const { return m_windowTitle; }
        inline const std::wstring& getWindowClassName() const { return m_windowClassName; }

    private:
        std::wstring m_windowTitle;
        std::wstring m_windowClassName;

        std::mutex m_mutex;
        std::condition_variable m_conditionVar;
        bool m_conditionFlag;
        HANDLE m_thread;

        HINSTANCE m_hInst;
        HWND m_hWnd;
    };

    HWND WindowCreate(HINSTANCE hInst)
    {
        return HwndCreator()(hInst);
    }
}

LRESULT CALLBACK DLLWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    if (message == WM_DESTROY)
    {
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProc(hwnd, message, wParam, lParam);
}

BOOL RegisterDLLWindowClass(LPCWSTR szClassName, Mocks::HwndCreator* creator)
{
    if (!creator)
        return false;

    WNDCLASSEX wc;

    wc.hInstance = creator->getHInstance();
    wc.lpszClassName = szClassName;
    wc.lpfnWndProc = DLLWindowProc;
    wc.cbSize = sizeof(WNDCLASSEX);

    wc.style = CS_DBLCLKS;
    wc.hIcon = LoadIcon(NULL, IDI_APPLICATION);
    wc.hIconSm = LoadIcon(NULL, IDI_APPLICATION);
    wc.hCursor = LoadCursor(NULL, IDC_ARROW);
    wc.lpszMenuName = NULL;
    wc.cbClsExtra = 0;
    wc.cbWndExtra = 0;
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_BACKGROUND);

    auto regRes = RegisterClassEx(&wc);
    return regRes;
}

DWORD WINAPI ThreadProc(LPVOID lpParam)
{
    MSG messages;
    Mocks::HwndCreator* creator = static_cast<Mocks::HwndCreator*>(lpParam);
    if (!creator)
        return static_cast<DWORD>(-1);

    if (RegisterDLLWindowClass(creator->getWindowClassName().c_str(), creator) != 0)
    {
        auto hWnd = CreateWindowEx(0, creator->getWindowClassName().c_str(), creator->getTitle().c_str(), WS_EX_APPWINDOW, CW_USEDEFAULT, CW_USEDEFAULT, 10, 10, nullptr, nullptr, creator->getHInstance(), NULL);
        SetWindowPos(hWnd, HWND_TOPMOST, 10, 10, 100, 100, SWP_SHOWWINDOW);
        creator->setHwnd(hWnd);
        creator->setCondition(true);

        while (GetMessage(&messages, NULL, 0, 0))
        {
            TranslateMessage(&messages);
            DispatchMessage(&messages);
        }

        creator->setHwnd(hWnd);
    }
    else
    {
        creator->setCondition(true);
    }

    return 1;
}

namespace Mocks
{
    HwndCreator::HwndCreator(const std::wstring& title) :
        m_windowTitle(title), m_windowClassName(std::to_wstring(++s_classId)), m_conditionFlag(false), m_thread(nullptr), m_hInst(HINSTANCE{}), m_hWnd(nullptr)
    {
    }

    HwndCreator::~HwndCreator()
    {
        std::unique_lock<std::mutex> lock(m_mutex);
        m_conditionVar.wait(lock, [this] { return m_conditionFlag; });

        if (m_thread)
        {
            CloseHandle(m_thread);
        }
    }

    HWND HwndCreator::operator()(HINSTANCE hInst)
    {
        m_hInst = hInst;
        m_conditionFlag = false;
        std::unique_lock<std::mutex> lock(m_mutex);

        m_thread = CreateThread(0, NULL, ThreadProc, reinterpret_cast<LPVOID>(this), NULL, NULL);
        m_conditionVar.wait(lock, [this] { return m_conditionFlag; });

        return m_hWnd;
    }

    void HwndCreator::setHwnd(HWND val)
    {
        m_hWnd = val;
    }

    void HwndCreator::setCondition(bool cond)
    {
        m_conditionFlag = cond;
        m_conditionVar.notify_one();
    }

}

std::wstring Helpers::GuidToString(const GUID& guid)
{
    OLECHAR* guidString;
    if (StringFromCLSID(guid, &guidString) == S_OK)
    {
        std::wstring guidStr{ guidString };
        CoTaskMemFree(guidString);
        return guidStr;
    }

    return L"";
}

std::wstring Helpers::CreateGuidString()
{
    GUID guid;
    if (CoCreateGuid(&guid) == S_OK)
    {
        return GuidToString(guid);
    }

    return L"";
}

std::optional<GUID> Helpers::StringToGuid(const std::wstring& str)
{
    GUID guid;
    if (CLSIDFromString(str.c_str(), &guid) == S_OK)
    {
        return guid;
    }

    return std::nullopt;
}
