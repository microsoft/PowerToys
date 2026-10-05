#pragma once

#include <shellapi.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/Result.h>
#include <WorkspacesLib/WorkspacesData.h>
#include <WorkspacesLib/SignatureVerification.h>
#include <WorkspacesLib/LaunchDecision.h>

namespace AppLauncher
{
    using ErrorList = std::vector<std::pair<std::wstring, std::wstring>>;

    enum class LaunchResult
    {
        Launched,
        Failed,
        Canceled,
        Skipped,
    };

    using ApprovalCallback = std::function<LaunchDecision(const std::wstring&, const std::wstring&, const SignatureVerification::Result&)>;

    struct LaunchError
    {
        DWORD code;
        std::wstring message;
    };

    LaunchResult Launch(const WorkspacesData::WorkspacesProject::Application& app, ErrorList& launchErrors, const ApprovalCallback& requestApproval, const std::function<bool()>& isCanceled);
    Result<SHELLEXECUTEINFO, LaunchError> LaunchApp(const std::wstring& appPath, const std::wstring& commandLineArgs, bool elevated);
}
