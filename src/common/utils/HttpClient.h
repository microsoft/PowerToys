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
        HttpClient()
        {
            auto headers = m_client.DefaultRequestHeaders();
            headers.UserAgent().TryParseAdd(USER_AGENT);
        }

        std::wstring request(const winrt::Windows::Foundation::Uri& url)
        {
            auto response = m_client.GetAsync(url).get();
            response.EnsureSuccessStatusCode();
            auto body = response.Content().ReadAsStringAsync().get();
            return std::wstring(body);
        }

        void download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFilePath)
        {
            auto response = m_client.GetAsync(url).get();
            response.EnsureSuccessStatusCode();
            auto file_stream = storage::Streams::FileRandomAccessStream::OpenAsync(dstFilePath.c_str(), storage::FileAccessMode::ReadWrite, storage::StorageOpenOptions::AllowReadersAndWriters, storage::Streams::FileOpenDisposition::CreateAlways).get();
            response.Content().WriteToStreamAsync(file_stream).get();
            file_stream.Close();
        }

        void download(const winrt::Windows::Foundation::Uri& url, const std::wstring& dstFilePath, const std::function<void(float)>& progressUpdateCallback)
        {
            auto response = m_client.GetAsync(url, HttpCompletionOption::ResponseHeadersRead).get();
            response.EnsureSuccessStatusCode();

            uint64_t totalBytes = response.Content().Headers().ContentLength().GetUInt64();
            auto contentStream = response.Content().ReadAsInputStreamAsync().get();

            uint64_t totalBytesRead = 0;
            storage::Streams::Buffer buffer(8192);
            auto fileStream = storage::Streams::FileRandomAccessStream::OpenAsync(dstFilePath.c_str(), storage::FileAccessMode::ReadWrite, storage::StorageOpenOptions::AllowReadersAndWriters, storage::Streams::FileOpenDisposition::CreateAlways).get();

            contentStream.ReadAsync(buffer, buffer.Capacity(), storage::Streams::InputStreamOptions::None).get();
            while (buffer.Length() > 0)
            {
                fileStream.WriteAsync(buffer).get();
                totalBytesRead += buffer.Length();
                if (progressUpdateCallback)
                {
                    float percentage = static_cast<float>(totalBytesRead) / totalBytes;
                    progressUpdateCallback(percentage);
                }

                contentStream.ReadAsync(buffer, buffer.Capacity(), storage::Streams::InputStreamOptions::None).get();
            }

            if (progressUpdateCallback)
            {
                progressUpdateCallback(1);
            }

            fileStream.Close();
            contentStream.Close();
        }

    private:
        winrt::Windows::Web::Http::HttpClient m_client;
    };
}
