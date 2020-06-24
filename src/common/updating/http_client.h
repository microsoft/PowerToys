#pragma once

#include <future>
#include <winrt/Windows.Web.Http.h>

namespace http
{
    class HttpClient
    {
    public:
        HttpClient();
        std::future<std::wstring> request(const winrt::Windows::Foundation::Uri& url);
        std::future<void> download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFle);
        std::future<void> download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFle, const std::function<void(float)>& progressUpdateCallback);

    private:
        winrt::Windows::Web::Http::HttpClient m_client;
    };
}
