// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved

#pragma once
#include "ShellHelpers.h"
#include "RegisterExtension.h"
#include <strsafe.h>
#include <new>  // std::nothrow

void DllAddRef();
void DllRelease();

// use UUDIGEN.EXE to generate unique CLSID values for your objects

class __declspec(uuid("dd2a27fa-7c7f-4b50-9b54-836af42fb64d")) CExplorerCommandVerb;
class __declspec(uuid("b3092d57-2ba5-469c-8110-1da4460b8d5b")) CExplorerCommandStateHandler;

HRESULT CExplorerCommandVerb_CreateInstance(REFIID riid, void **ppv);
HRESULT CExplorerCommandStateHandler_CreateInstance(REFIID riid, void **ppv);
HRESULT CExplorerCommandVerb_RegisterUnRegister(bool fRegister);
HRESULT CExplorerCommandStateHandler_RegisterUnRegister(bool fRegister);
