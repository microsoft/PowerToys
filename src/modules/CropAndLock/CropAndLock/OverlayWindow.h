#pragma once
#include <robmikh.common/DesktopWindow.h>

struct OverlayWindow : robmikh::common::desktop::DesktopWindow<OverlayWindow>
{
	static const std::wstring ClassName;
	OverlayWindow(
		winrt::Windows::UI::Composition::Compositor const& compositor, 
		HWND windowToCrop, 
		std::function<void(HWND, RECT)> windowCropped);
	~OverlayWindow() { m_windowCropped = nullptr;  DestroyWindow(m_window); }
	LRESULT MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam);

private:
	enum class CursorType
	{
		Standard,
		Crosshair,
	};

	enum class CropStatus
	{
		None,
		Ongoing,
		Completed,
	};

	static const float BorderThickness;
	static void RegisterWindowClass();

	void SetupOverlay();
	void ResetCrop();
	bool OnSetCursor();
	void OnLeftButtonDown(int x, int y);
	void OnLeftButtonUp(int x, int y);
	void OnMouseMove(int x, int y);

private:
	std::function<void(HWND, RECT)> m_windowCropped;
	winrt::Windows::UI::Composition::Compositor m_compositor{ nullptr };
	winrt::Windows::UI::Composition::CompositionTarget m_target{ nullptr };
	winrt::Windows::UI::Composition::ContainerVisual m_rootVisual{ nullptr };
	winrt::Windows::UI::Composition::SpriteVisual m_shadeVisual{ nullptr };
	winrt::Windows::UI::Composition::ContainerVisual m_windowAreaVisual{ nullptr };
	winrt::Windows::UI::Composition::SpriteVisual m_selectionVisual{ nullptr };
	winrt::Windows::UI::Composition::CompositionNineGridBrush m_shadeBrush{ nullptr };

	HWND m_currentWindow = nullptr;
	RECT m_currentWindowAreaBounds = {};

	CropStatus m_cropStatus = CropStatus::None;
	POINT m_startPosition = {};
	RECT m_cropRect = {};

	CursorType m_cursorType = CursorType::Standard;
	wil::unique_hcursor m_standardCursor;
	wil::unique_hcursor m_crosshairCursor;
};
