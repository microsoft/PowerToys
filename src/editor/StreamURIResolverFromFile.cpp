#include "pch.h"
#include "StreamUriResolverFromFile.h"

winrt::Windows::Foundation::IAsyncOperation<winrt::Windows::Storage::Streams::IInputStream> StreamUriResolverFromFile::UriToStreamAsync(const winrt::Windows::Foundation::Uri & uri) const {

  winrt::Windows::Storage::StorageFolder folder = winrt::Windows::Storage::StorageFolder::GetFolderFromPathAsync(winrt::param::hstring(base_path)).get();

  std::wstring myuri = uri.Path().c_str();
  myuri.erase(0, 1); // Removes the first slash from the URI

  std::replace(myuri.begin(), myuri.end(), '/', '\\');
  winrt::Windows::Storage::StorageFile file = nullptr;

  try {
    file = folder.GetFileAsync(winrt::param::hstring(myuri)).get();
  }
  catch (winrt::hresult_error const& e) {
    WCHAR message[1024] = L"";
    StringCchPrintf(message, ARRAYSIZE(message), L"failed: %ls", e.message().c_str());
    MessageBox(NULL, message, L"Error", MB_OK);
  }
 
  return file.OpenSequentialReadAsync();
}
