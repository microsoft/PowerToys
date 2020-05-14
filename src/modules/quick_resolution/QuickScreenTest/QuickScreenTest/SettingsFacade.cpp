#include "Resolution.h"

// TODO return this struct from getAllDisplaySettings once brightness setting is ready
//struct DisplaySettings {
//	char* displayName;
//	ResolutionSetting* possibleResolutions;
//	BrightnessSetting brightness;
//	// RECT displayCoordinates; TODO find this struct
//};

std::vector<MonitorResolutionSettings>* getAllDisplaySettings() {
	std::vector<MonitorResolutionSettings> resolutionSettings = getAllResolutionSettings();
	return &resolutionSettings;
}

bool SetResolution(WCHAR* displayName, int pixelWidth, int pixelHeight) {
	 if (setDisplayResolution(displayName, Resolution(pixelWidth, pixelHeight)))
		 return true;
	return false;
}


bool SetBrightness(char* displayName, int brightness) {
	return false;
}