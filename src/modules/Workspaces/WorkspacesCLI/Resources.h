// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <string>
#include <string_view>
#include "Generated Files/resource.h"
#include <WorkspacesLib/CliCommands.h>
#include "ConsoleApproval.h"

namespace CliResources
{
    inline std::wstring Get(UINT id)
    {
        const wchar_t* text = nullptr;
        const int length = LoadStringW(GetModuleHandleW(nullptr), id, reinterpret_cast<LPWSTR>(&text), 0);
        if (length <= 0)
            throw WorkspacesCli::Error(1, L"internalError", "Required command resources are unavailable.");
        return { text, static_cast<size_t>(length) };
    }

    inline UINT ErrorMessage(std::wstring_view code)
    {
        if (code == L"invalidArguments")
            return IDS_CLIINVALIDARGUMENTS;
        if (code == L"disabledByUser")
            return IDS_CLIDISABLEDBYUSER;
        if (code == L"disabledByPolicy")
            return IDS_CLIDISABLEDBYPOLICY;
        if (code == L"policyError")
            return IDS_CLIPOLICYERROR;
        if (code == L"invalidData")
            return IDS_CLIINVALIDDATA;
        if (code == L"storageError")
            return IDS_CLISTORAGEERROR;
        if (code == L"workspaceNotFound")
            return IDS_CLINOTFOUND;
        if (code == L"ambiguousName")
            return IDS_CLIAMBIGUOUSNAME;
        if (code == L"busy")
            return IDS_CLIBUSY;
        if (code == L"unavailable")
            return IDS_CLIUNAVAILABLE;
        if (code == L"unsupportedContext")
            return IDS_CLIUNSUPPORTEDCONTEXT;
        if (code == L"deElevationFailed")
            return IDS_CLIDEELEVATIONFAILED;
        if (code == L"consentDenied")
            return IDS_CLICONSENTDENIED;
        if (code == L"arrangerFailed")
            return IDS_CLIARRANGERFAILED;
        if (code == L"timeout")
            return IDS_CLITIMEOUT;
        if (code == L"outcomeUnknown")
            return IDS_CLIOUTCOMEUNKNOWN;
        if (code == L"canceled")
            return IDS_CLICANCELED;
        if (code == L"persistenceUnverified")
            return IDS_CLIPERSISTENCEUNVERIFIED;
        if (code == L"confirmationRequired")
            return IDS_CLICONFIRMATIONREQUIRED;
        if (code == L"confirmationTimedOut")
            return IDS_CLICONFIRMATIONTIMEDOUT;
        if (code == L"confirmationFailed")
            return IDS_CLICONFIRMATIONFAILED;
        if (code == L"skipped")
            return IDS_CLISKIPPED;
        return IDS_CLIINTERNALERROR;
    }

    inline void Localize(const json::JsonObject& output)
    {
        if (output.HasKey(L"error"))
        {
            const auto error = output.GetNamedObject(L"error");
            error.SetNamedValue(L"message", json::value(Get(ErrorMessage(error.GetNamedString(L"code").c_str()))));
        }
        if (output.HasKey(L"warnings"))
        {
            for (const auto& item : output.GetNamedArray(L"warnings"))
            {
                const auto warning = item.GetObjectW();
                const auto code = warning.GetNamedString(L"code");
                if (code == L"metadataSaveFailed" || code == L"metadataSaveConflict")
                    warning.SetNamedValue(L"message", json::value(Get(IDS_CLIMETADATAWARNING)));
            }
        }
        if (output.GetNamedString(L"command", L"") == L"launch" && output.HasKey(L"result"))
        {
            for (const auto& value : output.GetNamedObject(L"result").GetNamedArray(L"applications"))
            {
                const auto app = value.GetObjectW();
                const auto code = app.GetNamedString(L"code", L"");
                if (code == L"confirmationRequired" || code == L"confirmationTimedOut" ||
                    code == L"confirmationFailed" || code == L"skipped" || code == L"consentDenied" || code == L"canceled")
                    app.SetNamedValue(L"message", json::value(Get(ErrorMessage(code.c_str()))));
            }
        }
    }

    inline WorkspacesCli::ApprovalPromptText ApprovalText()
    {
        return {
            Get(IDS_CLIAPPROVALINTRODUCTION),
            Get(IDS_CLIAPPROVALAPP),
            Get(IDS_CLIAPPROVALPATH),
            Get(IDS_CLIAPPROVALARGUMENTS),
            Get(IDS_CLIAPPROVALREASON),
            Get(IDS_CLIAPPROVALSTATUS),
            Get(IDS_CLIAPPROVALCHOICES),
            Get(IDS_CLIAPPROVALINVALIDCHOICE),
        };
    }
}
