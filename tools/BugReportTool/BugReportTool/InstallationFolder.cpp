#include "InstallationFolder.h"

#include <fstream>
#include <set>
#include <Windows.h>
#include <common/utils/winapi_error.h>

using namespace std;
using std::filesystem::directory_iterator;
using std::filesystem::path;

wstring GetVersion(path filePath)
{
	DWORD  verHandle = 0;
	UINT   size = 0;
	LPVOID lpBuffer = nullptr;
	DWORD  verSize = GetFileVersionInfoSize(filePath.c_str(), &verHandle);
	wstring version = L"None";

	if (verSize != 0)
	{
		LPSTR verData = new char[verSize];

		if (GetFileVersionInfo(filePath.c_str(), verHandle, verSize, verData))
		{
			if (VerQueryValue(verData, L"\\", &lpBuffer, &size))
			{
				if (size)
				{
					VS_FIXEDFILEINFO* verInfo = static_cast<VS_FIXEDFILEINFO*>(lpBuffer);
					if (verInfo->dwSignature == 0xfeef04bd)
					{
						version =
							std::to_wstring((verInfo->dwFileVersionMS >> 16) & 0xffff) + L"." +
							std::to_wstring((verInfo->dwFileVersionMS >> 0) & 0xffff) + L"." +
							std::to_wstring((verInfo->dwFileVersionLS >> 16) & 0xffff) + L"." +
							std::to_wstring((verInfo->dwFileVersionLS >> 0) & 0xffff);
					}
				}
			}
		}

		delete[] verData;
	}

	return version;
}

optional<path> GetRootPath()
{
	WCHAR modulePath[MAX_PATH];
	if (!GetModuleFileName(NULL, modulePath, MAX_PATH))
	{
		return nullopt;
	}

	path rootPath = path(modulePath);
	rootPath = rootPath.remove_filename();
	rootPath = rootPath.append("..");
	return std::filesystem::canonical(rootPath);
}

wstring GetChecksum(path filePath)
{
	BOOL bResult = FALSE;
	HCRYPTPROV hProv = 0;
	HCRYPTHASH hHash = 0;
	HANDLE hFile = NULL;
	constexpr int bufferSize = 1024;
	BYTE rgbFile[bufferSize];
	DWORD cbRead = 0;
	constexpr int md5Length = 16;
	BYTE rgbHash[md5Length];
	DWORD cbHash = 0;
	CHAR rgbDigits[] = "0123456789abcdef";
	LPCWSTR filename = filePath.c_str();
	hFile = CreateFile(filename,
		GENERIC_READ,
		FILE_SHARE_READ,
		NULL,
		OPEN_EXISTING,
		FILE_FLAG_SEQUENTIAL_SCAN,
		NULL);

	if (INVALID_HANDLE_VALUE == hFile)
	{
		return L"CreateFile() failed. " + get_last_error_or_default(GetLastError());
	}

	// Get handle to the crypto provider
	if (!CryptAcquireContext(&hProv,
		NULL,
		NULL,
		PROV_RSA_FULL,
		CRYPT_VERIFYCONTEXT))
	{
		CloseHandle(hFile);
		return L"CryptAcquireContext() failed. " + get_last_error_or_default(GetLastError());
	}

	if (!CryptCreateHash(hProv, CALG_MD5, 0, 0, &hHash))
	{
		CloseHandle(hFile);
		CryptReleaseContext(hProv, 0);
		return L"CryptCreateHash() failed. " + get_last_error_or_default(GetLastError());
	}

	bResult = ReadFile(hFile, rgbFile, bufferSize, &cbRead, NULL);
	while (bResult)
	{
		if (0 == cbRead)
		{
			break;
		}

		if (!CryptHashData(hHash, rgbFile, cbRead, 0))
		{
			CryptReleaseContext(hProv, 0);
			CryptDestroyHash(hHash);
			CloseHandle(hFile);
			return L"CryptHashData() failed. " + get_last_error_or_default(GetLastError());;
		}

		bResult = ReadFile(hFile, rgbFile, bufferSize, &cbRead, NULL);
	}

	if (!bResult)
	{
		CryptReleaseContext(hProv, 0);
		CryptDestroyHash(hHash);
		CloseHandle(hFile);
		return L"ReadFile() failed. " + get_last_error_or_default(GetLastError());;
	}

	cbHash = md5Length;
	std::wstring result = L"";
	if (CryptGetHashParam(hHash, HP_HASHVAL, rgbHash, &cbHash, 0))
	{
		for (DWORD i = 0; i < cbHash; i++)
		{
			result += rgbDigits[rgbHash[i] >> 4];
			result += rgbDigits[rgbHash[i] & 0xf];
		}
	}
	else
	{
		result = L"CryptGetHashParam() failed. " + get_last_error_or_default(GetLastError());;
	}

	CryptDestroyHash(hHash);
	CryptReleaseContext(hProv, 0);
	CloseHandle(hFile);

	return result;
}

class Reporter
{
private:
	std::wofstream os;
	std::wofstream GetOutputStream(const path& tmpDir)
	{
		auto path = tmpDir;
		path += "installationFolderStructure.txt";
		std::wofstream out_s = std::wofstream(path);
		return out_s;
	}
public:
	Reporter(const path& tmpDir)
	{
		os = GetOutputStream(tmpDir);
	}

	void Report(path dirPath, int indentation = 0)
	{
		set<pair<path, bool>> paths;
		try
		{
			directory_iterator end_it;
			for (directory_iterator it(dirPath); it != end_it; ++it)
			{
				paths.insert({ it->path(), it->is_directory() });
			}
		}
		catch (filesystem::filesystem_error err)
		{
			os << err.what() << endl;
		}

		for (auto filePair : paths)
		{
			auto filePath = filePair.first;
			auto isDirectory = filePair.second;

			auto fileName = filePath.wstring().substr(dirPath.wstring().size() + 1);
			os << wstring(indentation, ' ') << fileName << " ";
			if (!isDirectory)
			{
				os << GetVersion(filePath) << " " << GetChecksum(filePath);
			}

			os << endl;
			if (isDirectory)
			{
				Report(filePath, indentation + 2);
			}
		}
	}
};

void InstallationFolder::ReportStructure(const path& tmpDir)
{
	auto rootPath = GetRootPath();
	if (rootPath)
	{
		Reporter(tmpDir).Report(rootPath.value());
	}
}
