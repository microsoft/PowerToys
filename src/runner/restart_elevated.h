#pragma once
void schedule_restart_as_elevated();
void schedule_restart_as_non_elevated();
bool is_restart_scheduled();
bool restart_if_scheduled();
