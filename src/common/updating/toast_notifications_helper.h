#pragma once

namespace updating
{
    struct new_version_download_info;
}

namespace notifications
{
    void show_unavailable();
    void show_download_start();
    void show_visit_github(const updating::new_version_download_info& info);
    void show_install_error(const updating::new_version_download_info& info);
    void show_version_ready(const updating::new_version_download_info& info);
    void show_version_ready_to_install_immediately(const updating::new_version_download_info& info);
    void show_uninstallation_success();
    void show_uninstallation_error();

    void update_download_progress(float progress);
}