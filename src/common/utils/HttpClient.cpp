#include "pch.h"
#include "HttpClient.h"

namespace http
{
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
        auto fileStream = co_await storage::Streams::FileRandomAccessStream::OpenAsync(
            dstFilePath.c_str(),
            storage::FileAccessMode::ReadWrite,
            storage::StorageOpenOptions::AllowReadersAndWriters,
            storage::Streams::FileOpenDisposition::CreateAlways);
        co_await response.Content().WriteToStreamAsync(fileStream);
        fileStream.Close();
    }

    std::future<void> HttpClient::download(const winrt::Windows::Foundation::Uri& url,
                                           const std::wstring& dstFilePath,
                                           const std::function<void(float)>& progressUpdateCallback)
    {
        auto response = co_await m_client.GetAsync(url, winrt::Windows::Web::Http::HttpCompletionOption::ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        const uint64_t totalBytes = response.Content().Headers().ContentLength().GetUInt64();
        auto contentStream = co_await response.Content().ReadAsInputStreamAsync();

        uint64_t totalBytesRead = 0;
        storage::Streams::Buffer buffer(8192);
        auto fileStream = co_await storage::Streams::FileRandomAccessStream::OpenAsync(
            dstFilePath.c_str(),
            storage::FileAccessMode::ReadWrite,
            storage::StorageOpenOptions::AllowReadersAndWriters,
            storage::Streams::FileOpenDisposition::CreateAlways);

        co_await contentStream.ReadAsync(buffer, buffer.Capacity(), storage::Streams::InputStreamOptions::None);
        while (buffer.Length() > 0)
        {
            co_await fileStream.WriteAsync(buffer);
            totalBytesRead += buffer.Length();
            if (progressUpdateCallback)
            {
                const float percentage = static_cast<float>(totalBytesRead) / totalBytes;
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
