#pragma once

#include "pch.h"

struct ProcessResult
{
	std::wstring name;
	DWORD pid;
	uint64_t num_files;
};

// First version, checks handles towards the given objects themselves.
// In particular, if the object is a directory, its entries are not considered
std::vector<ProcessResult> find_processes_nonrecursive(const std::vector<std::wstring>& paths);

// Second version, checks handles towards files and all subfiles and folders of given dirs, if any.
std::vector<ProcessResult> find_processes_recursive(const std::vector<std::wstring>& paths);
