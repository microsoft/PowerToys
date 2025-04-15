#pragma once

class KeyboardInput
{
public:
	struct Key
	{
        USHORT vkKey{};
        bool pressed{};
	};

	KeyboardInput() = default;
    ~KeyboardInput() = default;

	static bool Initialize(HWND window);
	static std::optional<Key> OnKeyboardInput(HRAWINPUT hInput);
};
