#include "Resolution.h"

struct BrightnessSetting {
	int maxBrightness;
	int currentBrightness;
};

struct ResolutionSetting {
	Resolution* possibleResolutions;
	Resolution currentResolution;
};


struct DisplaySettings {
	char* displayName;
	ResolutionSetting* possibleResolutions;
	BrightnessSetting brightness;
	// RECT displayCoordinates; TODO find this struct
};


bool SetResolution(char* displayName, int pixelWidth, int pixelHeight) {
	return false;
}


bool SetBrightness(char* displayName, int brightness) {
	return false;
}

DisplaySettings* getAllDisplaySettings() {
	return 0;
}