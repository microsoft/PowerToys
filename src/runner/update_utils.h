#pragma once

#include <common/updating/updating.h>

bool start_msi_uninstallation_sequence();
void github_update_worker();
std::optional<updating::github_version_info> check_for_updates();
void proceed_with_update(const updating::new_version_download_info& download_info, const bool download_update);
bool launch_pending_update();