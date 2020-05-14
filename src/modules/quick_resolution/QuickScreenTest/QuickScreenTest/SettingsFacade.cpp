#include "Resolution.h"
#include <iostream>

// TODO return this struct from getAllDisplaySettings once brightness setting is ready
//struct DisplaySettings {
//	char* displayName;
//	ResolutionSetting* possibleResolutions;
//	BrightnessSetting brightness;
//	// RECT displayCoordinates; TODO find this struct
//};

extern "C" __declspec(dllexport) bool SetResolution(WCHAR * displayName, int pixelWidth, int pixelHeight);


extern "C" __declspec(dllexport) void hello();


void hello() {
	std::cout << "hello";
}

MonitorResolutionSettings* getAllDisplaySettings() {
	MonitorResolutionSettings* resolutionSettings = getAllResolutionSettings();
	return resolutionSettings;
}

bool SetResolution(WCHAR* displayName, int pixelWidth, int pixelHeight) {
	 if (setDisplayResolution(displayName, Resolution(pixelWidth, pixelHeight)))
		 return true;
	return false;
}


bool SetBrightness(char* displayName, int brightness) {
	return false;
}