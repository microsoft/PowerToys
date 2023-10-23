#pragma once

class KeyboardInput
{
public: 
	KeyboardInput() = default;
    ~KeyboardInput() = default;

	static bool Initialize(HWND window);
};
