#pragma once
void schedule_restart_as_elevated(bool openSettings);
void schedule_restart_as_non_elevated();
void schedule_restart_as_non_elevated(bool openSettings);
bool is_restart_scheduled();
bool restart_if_scheduled();
bool restart_same_elevation();
