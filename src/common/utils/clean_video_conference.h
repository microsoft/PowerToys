#pragma once

#include <windows.h>

// Video Conference Mute was a utility we deprecated. However, this required a manual user disable of the module to remove the camera registration, so we include the disable code here to be able to clean up.
void clean_video_conference();
