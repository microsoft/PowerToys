#include "pch.h"
#include "http_client.h"

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Web.Http.Headers.h>
#include <winrt/Windows.Storage.Streams.h>

namespace http
{
    using namespace winrt::Windows::Web::Http;
    namespace storage = winrt::Windows::Storage;

    const wchar_t USER_AGENT[] = L"Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";

    HttpClient::HttpClient()
    {
        auto headers = m_client.DefaultRequestHeaders();
        headers.UserAgent().TryParseAdd(USER_AGENT);
    }

    std::future<std::wstring> HttpClient::request(const winrt::Windows::Foundation::Uri& url)
    {
        auto response = co_await m_client.GetAsync(url);
        (void)response.EnsureSuccessStatusCode();
        auto body = co_await response.Content().ReadAsStringAsync();
        co_return std::wstring(body);
    }

    std::future<void> HttpClient::download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFilePath)
    {
        auto response = co_await m_client.GetAsync(url);
        (void)response.EnsureSuccessStatusCode();
        auto file_stream = co_await storage::Streams::FileRandomAccessStream::OpenAsync(dstFilePath.c_str(), storage::FileAccessMode::ReadWrite, storage::StorageOpenOptions::AllowReadersAndWriters, storage::Streams::FileOpenDisposition::CreateAlways);
        co_await response.Content().WriteToStreamAsync(file_stream);
        file_stream.Close();
    }

    std::future<void> HttpClient::download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFilePath, const std::function<void(float)>& progressUpdateCallback)
    {
        auto response = co_await m_client.GetAsync(url, HttpCompletionOption::ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        uint64_t totalBytes = response.Content().Headers().ContentLength().GetUInt64();
        auto contentStream = co_await response.Content().ReadAsInputStreamAsync();

        uint64_t totalBytesRead = 0;
        storage::Streams::Buffer buffer(8192);
        auto fileStream = co_await storage::Streams::FileRandomAccessStream::OpenAsync(dstFilePath.c_str(), storage::FileAccessMode::ReadWrite, storage::StorageOpenOptions::AllowReadersAndWriters, storage::Streams::FileOpenDisposition::CreateAlways);

        co_await contentStream.ReadAsync(buffer, buffer.Capacity(), storage::Streams::InputStreamOptions::None);
        while (buffer.Length() > 0)
        {
            co_await fileStream.WriteAsync(buffer);
            totalBytesRead += buffer.Length();
            if (progressUpdateCallback)
            {
                float percentage = (float)totalBytesRead / totalBytes;
                progressUpdateCallback(percentage);
            }

            co_await contentStream.ReadAsync(buffer, buffer.Capacity(), storage::Streams::InputStreamOptions::None);
        }

        if (progressUpdateCallback)
        {
            progressUpdateCallback(1);
        }

        fileStream.Close();
        contentStream.Close();
    }
}