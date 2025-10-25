#pragma once

#include <future>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Storage.Streams.h>
#include <winrt/Windows.Web.Http.h>
#include <winrt/Windows.Web.Http.Headers.h>
namespace http
{
    using namespace winrt::Windows::Web::Http;
    namespace storage = winrt::Windows::Storage;

    const inline wchar_t USER_AGENT[] = L"Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";

    class HttpClient
    {
    public:
        HttpClient();

        std::future<std::wstring> request(const winrt::Windows::Foundation::Uri& url);
        std::future<void> download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFilePath);
        std::future<void> download(const winrt::Windows::Foundation::Uri& url,
                                   const std::wstring& dstFilePath,
                                   const std::function<void(float)>& progressUpdateCallback);

    private:
        winrt::Windows::Web::Http::HttpClient m_client;
    };
}
