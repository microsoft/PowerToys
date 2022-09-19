#pragma once

#include "pch.h"

namespace ipc
{
	class Writer
	{
	public:
		Writer();
		~Writer();
		HRESULT start();
		HRESULT add_path(LPCWSTR path);
		void finish();
		HANDLE get_read_handle();
	private:
		HANDLE m_read_pipe = NULL;
		HANDLE m_write_pipe = NULL;
	};

	std::vector<std::wstring> read_paths_from_stdin();
}
