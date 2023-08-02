#pragma once
#include <robmikh.common/DesktopWindow.h>

struct ChildWindow : robmikh::common::desktop::DesktopWindow<ChildWindow>
{
	static const std::wstring ClassName;
	ChildWindow(int width, int height, HWND parent);
	LRESULT MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam);
private:
	static void RegisterWindowClass();
};