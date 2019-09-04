#pragma once
#include <cpprest/json.h>

web::json::value get_general_settings();
void apply_general_settings(const web::json::value& general_configs);
void start_initial_powertoys();
