#include "../Common/LsmrCommon.h"

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/base.h>

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        const auto fullName = ptlsmr::argument_value(arguments, L"--register-package");
        if (!ptlsmr::is_allowed_package_full_name(fullName) ||
            ptlsmr::current_token_user_sid() != L"S-1-5-19")
        {
            return ERROR_ACCESS_DENIED;
        }

        winrt::init_apartment(winrt::apartment_type::multi_threaded);
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto dependencies = winrt::single_threaded_vector<winrt::hstring>().GetView();
        const auto result = manager.RegisterPackageByFullNameAsync(
            fullName,
            dependencies,
            winrt::Windows::Management::Deployment::DeploymentOptions::ForceUpdateFromAnyVersion)
                                .get();
        if (FAILED(result.ExtendedErrorCode()))
        {
            return static_cast<int>(HRESULT_CODE(result.ExtendedErrorCode()));
        }
        return ERROR_SUCCESS;
    }
    catch (const ptlsmr::win32_error& error)
    {
        return static_cast<int>(error.code());
    }
    catch (const winrt::hresult_error& error)
    {
        return static_cast<int>(HRESULT_CODE(error.code()));
    }
    catch (...)
    {
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
