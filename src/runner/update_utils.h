#pragma once

#include <common/updating/updating.h>

bool start_msi_uninstallation_sequence();
void github_update_worker();
std::optional<updating::new_version_download_info> check_for_updates();
bool launch_pending_update();