#pragma once

#include <common/json.h>

json::JsonObject load_general_settings();
json::JsonObject get_general_settings();
void apply_general_settings(const json::JsonObject & general_configs);
void start_initial_powertoys();
