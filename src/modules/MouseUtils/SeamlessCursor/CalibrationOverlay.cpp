// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CalibrationOverlay.h"

#include "CalibrationStore.h"
#include "Monitors.h"
#include "OverlaySnapshot.h"
#include "SeamlessCursorEngine.h"
#include "Generated Files/resource.h"
#include "trace.h"

#include <common/logger/logger.h>
#include <common/utils/resources.h>

#include <cmath>
#include <d2d1.h>
#include <deque>
#include <dwrite.h>
#include <system_error>
#include <tuple>
#include <variant>
#include <windowsx.h>
#include <winrt/base.h>

namespace seamless_cursor
{
    namespace
    {
        constexpr wchar_t windowClassName[] = L"PowerToys.SeamlessCursor.Calibration";
        constexpr UINT closeMessage = WM_APP + 1;
        constexpr UINT bringToFrontMessage = WM_APP + 2;
        constexpr BYTE windowOpacity = 250;

        constexpr UINT_PTR animationTimer = 1;
        constexpr UINT animationFrameMs = 16;
        constexpr ULONGLONG animationLoopMs = 1600;
        constexpr ULONGLONG animationLoops = 4; // Alternating between pair 1 on monitor B and pair 2 on monitor A.
        constexpr double animationOffset = 0.12; // Ghost start, as a fraction of the edge length.

        // Pointer trail: glowing dots that shrink, cool from white to blue and fade out.
        constexpr UINT_PTR trailTimer = 2;
        constexpr ULONGLONG trailLifetimeMs = 1200;
        constexpr float trailSpacing = 7.0f; // Between dots, so a fast move leaves no gaps.
        constexpr float trailMaxRadius = 4.0f; // Of a new dot; it shrinks to the minimum as it fades.
        constexpr float trailMinRadius = 1.0f;
        constexpr float trailGlowRatio = 2.6f; // Glow radius, relative to the dot.
        constexpr float trailGlowAlpha = 0.16f; // Overlapping glows add up to a soft band.
        constexpr size_t trailMaxDots = 2000; // Per monitor, so a long scribble stays cheap to draw.
        constexpr UINT32 trailColor = 0x60CDFF;

        // A CursorWrap wrap draws a fading line at the same physical height (or position) across the monitors it wraps
        // over, so the landing point can be checked against it.
        constexpr ULONGLONG wrapLineLifetimeMs = 1800;
        constexpr float wrapLineWidth = 1.5f;
        constexpr float wrapLineGlowWidth = 8.0f;
        constexpr float wrapLineGlowAlpha = 0.2f;

        // Sizes in pixels at 100 % scaling.
        constexpr float markerThickness = 24.0f;
        constexpr float markerLengthRatio = 0.1f; // Of the monitor size across the edge.
        constexpr float markerHitMargin = 8.0f;
        constexpr float toggleWidth = 240.0f;
        constexpr float glueToggleWidth = 360.0f;
        constexpr float endLineLengthRatio = 0.2f; // Of the monitor size across the edge.
        constexpr float edgeMapThickness = 6.0f;

        // A strip along the inside of an edge, from `start` to `end` along it, in client coordinates.
        D2D1_RECT_F EdgeStrip(const RECT& rect, Edge edge, float start, float end, float thickness) noexcept
        {
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);
            switch (edge)
            {
            case Edge::Top:
                return { start, 0.0f, end, thickness };
            case Edge::Bottom:
                return { start, height - thickness, end, height };
            case Edge::Left:
                return { 0.0f, start, thickness, end };
            default:
                return { width - thickness, start, width, end };
            }
        }
        constexpr float toggleHeight = 36.0f;
        constexpr float buttonWidth = 120.0f;
        constexpr float previewToggleWidth = 220.0f;
        constexpr float buttonHeight = 36.0f;
        constexpr double largeStepPixels = 10.0;
        constexpr float switchGap = 40.0f; // Between the lines along an edge and the switch.
        constexpr float issueGap = 8.0f;
        constexpr float issueHeight = 48.0f;
        constexpr float issueOverhang = 80.0f; // How far the issue text reaches past each end of the switch.
        constexpr float markerLabelSize = 24.0f;
        constexpr float captionPosition = 0.3f; // Of the monitor height.
        constexpr float captionHeight = 88.0f; // Monitor name and resolution, with the space below them.
        constexpr float panelMaxWidth = 760.0f;
        constexpr float panelMinWidth = 500.0f; // The padding and a row of two switches in DrawPanel.
        constexpr float panelHeight = 390.0f;
        constexpr float compactPanelHeight = 212.0f; // Title, key hints, a row of switches and the buttons.
        constexpr float panelPadding = 24.0f;
        constexpr size_t panelSwitchCount = 4; // Try while adjusting, Pointer trail, Edge map and Stop at empty edges.
        constexpr size_t panelSwitchesPerGroup = 2; // Trying out (the first two) and the edges (the last two) share a row.
        constexpr float panelSpacing = 12.0f; // Between the panel's switches and buttons.

        // Usual room for the panel texts; the panel grows when they need more (see MeasurePanelText).
        constexpr float instructionsHeight = 130.0f;
        constexpr float keysHeight = 28.0f;
        constexpr float legendHeight = 48.0f;
        constexpr float panelGap = 16.0f;

        constexpr UINT32 backgroundColor = 0x1C1C1C;
        constexpr UINT32 panelColor = 0x2B2B2B;
        constexpr UINT32 buttonColor = 0x3D3D3D;
        constexpr UINT32 accentColor = 0x0078D4;
        constexpr UINT32 textColor = 0xFFFFFF;
        constexpr UINT32 secondaryTextColor = 0xC8C8C8;
        constexpr UINT32 disabledColor = 0x6E6E6E;
        constexpr UINT32 warningColor = 0xFFB900;
        constexpr UINT32 errorColor = 0xFF6B6B;

        // Okabe-Ito palette: distinguishable with common color vision deficiencies.
        constexpr std::array<UINT32, 7> borderColors{ 0xE69F00, 0x56B4E9, 0x009E73, 0xF0E442, 0x0072B2, 0xD55E00, 0xCC79A7 };

        enum class Action
        {
            Save,
            Reset,
            Cancel,
            TogglePreview,
            ToggleEdgeMap,
            ToggleStop,
            ToggleTrail,
        };

        struct Strings
        {
            std::wstring title = GET_RESOURCE_STRING(IDS_SEAMLESS_CURSOR_NAME);
            std::wstring instructions = GET_RESOURCE_STRING(IDS_OVERLAY_INSTRUCTIONS_ALIGN) + L"\n" + GET_RESOURCE_STRING(IDS_OVERLAY_INSTRUCTIONS_TRY) +
                                        L"\n" + GET_RESOURCE_STRING(IDS_OVERLAY_INSTRUCTIONS_SWITCHES);
            std::array<std::wstring, 5> keyHints{
                GET_RESOURCE_STRING(IDS_OVERLAY_KEY_TAB),
                GET_RESOURCE_STRING(IDS_OVERLAY_KEY_ARROWS),
                GET_RESOURCE_STRING(IDS_OVERLAY_KEY_SPACE),
                GET_RESOURCE_STRING(IDS_OVERLAY_KEY_ENTER),
                GET_RESOURCE_STRING(IDS_OVERLAY_KEY_ESC),
            };
            std::wstring noBorders = GET_RESOURCE_STRING(IDS_OVERLAY_NO_BORDERS);
            std::wstring monitorsUnknown = GET_RESOURCE_STRING(IDS_OVERLAY_MONITORS_UNKNOWN);
            std::wstring save = GET_RESOURCE_STRING(IDS_OVERLAY_SAVE);
            std::wstring reset = GET_RESOURCE_STRING(IDS_OVERLAY_RESET);
            std::wstring cancel = GET_RESOURCE_STRING(IDS_OVERLAY_CANCEL);
            std::wstring close = GET_RESOURCE_STRING(IDS_OVERLAY_CLOSE);
            std::wstring crossingOn = GET_RESOURCE_STRING(IDS_OVERLAY_CROSSING_ON);
            std::wstring crossingOff = GET_RESOURCE_STRING(IDS_OVERLAY_CROSSING_OFF);
            std::wstring tooClose = GET_RESOURCE_STRING(IDS_OVERLAY_ISSUE_TOO_CLOSE);
            std::wstring invalid = GET_RESOURCE_STRING(IDS_OVERLAY_ISSUE_INVALID);
            std::wstring reversed = GET_RESOURCE_STRING(IDS_OVERLAY_ISSUE_REVERSED);
            std::wstring scale = GET_RESOURCE_STRING(IDS_OVERLAY_ISSUE_SCALE);
            std::wstring saveFailed = GET_RESOURCE_STRING(IDS_OVERLAY_SAVE_FAILED);
            std::wstring saveBlocked = GET_RESOURCE_STRING(IDS_OVERLAY_SAVE_BLOCKED);
            std::wstring previewOn = GET_RESOURCE_STRING(IDS_OVERLAY_PREVIEW_ON);
            std::wstring previewOff = GET_RESOURCE_STRING(IDS_OVERLAY_PREVIEW_OFF);
            std::wstring glueOn = GET_RESOURCE_STRING(IDS_OVERLAY_GLUE_ON);
            std::wstring glueOff = GET_RESOURCE_STRING(IDS_OVERLAY_GLUE_OFF);
            std::wstring mapOn = GET_RESOURCE_STRING(IDS_OVERLAY_MAP_ON);
            std::wstring mapOff = GET_RESOURCE_STRING(IDS_OVERLAY_MAP_OFF);
            std::wstring trailOn = GET_RESOURCE_STRING(IDS_OVERLAY_TRAIL_ON);
            std::wstring trailOff = GET_RESOURCE_STRING(IDS_OVERLAY_TRAIL_OFF);
            std::wstring stopOn = GET_RESOURCE_STRING(IDS_OVERLAY_STOP_ON);
            std::wstring stopOff = GET_RESOURCE_STRING(IDS_OVERLAY_STOP_OFF);
            std::wstring mapLegend = GET_RESOURCE_STRING(IDS_OVERLAY_MAP_LEGEND);
        };

        // Fills the {0} placeholder of a localized string.
        std::wstring WithName(std::wstring text, const std::wstring& name)
        {
            if (const auto at = text.find(L"{0}"); at != std::wstring::npos)
            {
                text.replace(at, 3, name);
            }

            return text;
        }

        HINSTANCE ModuleInstance() noexcept
        {
            return reinterpret_cast<HINSTANCE>(&__ImageBase);
        }

        D2D1_COLOR_F Color(UINT32 rgb, float alpha = 1.0f) noexcept
        {
            return D2D1::ColorF(rgb, alpha);
        }

        // `from` blended toward `to` by `amount` (0 to 1).
        D2D1_COLOR_F Blend(UINT32 from, UINT32 to, float amount, float alpha) noexcept
        {
            const D2D1_COLOR_F a = Color(from);
            const D2D1_COLOR_F b = Color(to);
            return { a.r + (b.r - a.r) * amount, a.g + (b.g - a.g) * amount, a.b + (b.b - a.b) * amount, alpha };
        }

        bool Contains(const D2D1_RECT_F& rect, POINT pt) noexcept
        {
            return pt.x >= rect.left && pt.x < rect.right && pt.y >= rect.top && pt.y < rect.bottom;
        }

        D2D1_RECT_F Inflate(const D2D1_RECT_F& rect, float amount) noexcept
        {
            return { rect.left - amount, rect.top - amount, rect.right + amount, rect.bottom + amount };
        }

        bool Overlaps(const D2D1_RECT_F& a, const D2D1_RECT_F& b) noexcept
        {
            return a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom;
        }

        // Where the issue text of a switch goes: below it, or above it on a bottom edge, kept on the monitor when the
        // switch sits near its side.
        D2D1_RECT_F IssueRect(const D2D1_RECT_F& toggle, Edge edge, float scale, float monitorWidth) noexcept
        {
            const float gap = issueGap * scale;
            const float height = issueHeight * scale;
            const float overhang = issueOverhang * scale;
            D2D1_RECT_F rect{ toggle.left - overhang, toggle.bottom + gap, toggle.right + overhang, toggle.bottom + gap + height };
            if (edge == Edge::Bottom)
            {
                rect.top = toggle.top - gap - height;
                rect.bottom = toggle.top - gap;
            }

            const float inward = std::max(0.0f, -rect.left) - std::max(0.0f, rect.right - monitorWidth);
            rect.left += inward;
            rect.right += inward;
            return rect;
        }

        // The panel's switches go in rows of related pairs above the buttons, or one per row where a pair does not fit.
        size_t PanelSwitchesPerRow(float panelWidth, float scale) noexcept
        {
            const float room = panelWidth - 2.0f * panelPadding * scale + panelSpacing * scale;
            const auto fitting = static_cast<size_t>(std::max(0.0f, room / ((previewToggleWidth + panelSpacing) * scale)));
            return std::clamp<size_t>(fitting, 1, panelSwitchesPerGroup);
        }

        size_t PanelSwitchRows(float panelWidth, float scale) noexcept
        {
            const size_t perRow = PanelSwitchesPerRow(panelWidth, scale);
            return (panelSwitchCount + perRow - 1) / perRow;
        }

        // A switch with the room its issue text can take.
        D2D1_RECT_F SwitchFootprint(const D2D1_RECT_F& toggle, Edge edge, float scale, float monitorWidth) noexcept
        {
            const auto issue = IssueRect(toggle, edge, scale, monitorWidth);
            return { std::min(toggle.left, issue.left), std::min(toggle.top, issue.top), std::max(toggle.right, issue.right), std::max(toggle.bottom, issue.bottom) };
        }

        // Settings is in the foreground when the user clicks Calibrate; an empty input event lets this
        // process take the foreground, so the overlay gets keyboard focus.
        void BringToForeground(HWND window) noexcept
        {
            INPUT input{};
            input.type = INPUT_MOUSE;
            SendInput(1, &input, sizeof(input));
            SetForegroundWindow(window);
        }

        // A marker is identified by its border, its pair (0 or 1) and the monitor it is on.
        struct MarkerRef
        {
            size_t border = 0;
            size_t pair = 0;
            bool onB = false;

            bool operator==(const MarkerRef&) const = default;
        };

        // A translucent copy of a marker that the hint animation slides into place.
        struct Ghost
        {
            MarkerRef marker;
            double fraction = 0.0;
        };

        // The switch of a border; a glued pair shows it on both monitors.
        struct SwitchRef
        {
            size_t border = 0;

            bool operator==(const SwitchRef&) const = default;
        };

        // Where Tab moves the keyboard focus: a line, a border's switch, or a switch or button on the panel.
        using FocusStop = std::variant<MarkerRef, SwitchRef, Action>;

        struct OverlayBorder
        {
            size_t a = 0; // Indexes into the overlay windows.
            size_t b = 0;
            Edge edgeOfA = Edge::Top;
            int sharedStart = 0; // Range Windows connects; for a glue candidate, the range where the monitors face.
            int sharedEnd = 0;
            bool glue = false; // Facing across a gap Windows cannot close; enabled means glued.
            bool enabled = true;
            std::array<MarkerPair, 2> pairs{};
            bool secondPairMoved = false; // Until then, pair 2 follows pair 1 using the reported monitor sizes.

            // For a glue candidate: its lines are worth saving even while unglued, because they were stored before, or
            // glued or moved in this session. Saved unglued, they come back when the pair is glued again.
            bool keep = false;

            // Whether its stored record is a glue record. It changes only when the border's switch is used or on Reset,
            // so saving other borders does not turn a shared border that was switched off into an unglued pair.
            bool savedAsGlue = false;
            UINT32 color = 0;
            std::array<D2D1_RECT_F, 2> switches{}; // On monitors a and b, where HasSwitch; see LayOutSwitches.
        };

        struct TrailDot
        {
            D2D1_POINT_2F point{};
            ULONGLONG time = 0;
        };

        // A line across the whole monitor after a CursorWrap wrap: horizontal at a client y, or vertical at a client x.
        struct WrapLine
        {
            bool horizontal = true;
            float position = 0.0f;
            ULONGLONG time = 0;
        };

        // How a stored calibration relates to a border on screen.
        enum class SavedMatch
        {
            None,
            Same,
            Swapped, // Stored with monitors A and B the other way around.
        };

        struct OverlayWindow
        {
            MonitorDetails details;
            HWND hwnd = nullptr;
            winrt::com_ptr<IWICBitmap> bitmap; // Drawn into instead of a window in a snapshot (see RenderSnapshot).
            float scale = 1.0f;
            winrt::com_ptr<ID2D1RenderTarget> target;
            winrt::com_ptr<ID2D1SolidColorBrush> brush;
            winrt::com_ptr<IDWriteTextFormat> captionFormat;
            winrt::com_ptr<IDWriteTextFormat> titleFormat;
            winrt::com_ptr<IDWriteTextFormat> textFormat;
            winrt::com_ptr<IDWriteTextFormat> centeredFormat;
            winrt::com_ptr<IDWriteTextFormat> pillFormat; // One line, trimmed when a monitor name is long.

            // Issue text below and above a switch: aligned to the switch, so a long one grows away from it.
            winrt::com_ptr<IDWriteTextFormat> issueBelowFormat;
            winrt::com_ptr<IDWriteTextFormat> issueAboveFormat;
            std::vector<std::pair<D2D1_RECT_F, Action>> buttons;
            std::deque<TrailDot> trail; // Pointer trail, in client coordinates, oldest first.
            std::deque<WrapLine> wrapLines; // Oldest first.
            bool trailTimerRunning = false;
        };

        // All overlay state. Lives on the overlay thread only.
        class Session
        {
        public:
            Session(const CalibrationOverlayCallbacks& callbacks, EngineOptions engineOptions) :
                m_callbacks(callbacks), m_engineOptions(engineOptions)
            {
            }

            Session(const Session&) = delete;
            Session& operator=(const Session&) = delete;

            ~Session()
            {
                DestroyWindows();
                UnregisterClassW(windowClassName, ModuleInstance());
            }

            bool Open();
            std::vector<winrt::com_ptr<IWICBitmap>> Snapshot(IWICImagingFactory* wic, const SnapshotRequest& request);
            void Finish(bool saved);
            void BringToFront();

            bool Saved() const noexcept
            {
                return m_saved;
            }

            bool Finished() const noexcept
            {
                return m_finished;
            }

            static LRESULT CALLBACK WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);

        private:
            LRESULT HandleMessage(size_t index, UINT message, WPARAM wParam, LPARAM lParam);
            bool Prepare(std::vector<MonitorDetails> monitors, const OverlayPreferences& preferences, const std::function<std::optional<std::vector<BorderCalibration>>()>& loadSaved);
            void CreateFormats(OverlayWindow& window);
            ULONGLONG Now() const;

            // Model.
            void CreateBorders(std::optional<std::vector<BorderCalibration>> saved);
            bool ApplySaved(OverlayBorder& border, const std::vector<BorderCalibration>& saved) const;
            SavedMatch MatchSaved(const BorderCalibration& calibration, const OverlayBorder& border) const;
            std::array<MarkerPair, 2> Predict(const OverlayBorder& border) const;
            void PredictSecondPair(OverlayBorder& border) const;
            double PredictedScale(const OverlayBorder& border) const;
            BorderCalibration ToCalibration(const OverlayBorder& border) const;
            CalibrationIssue Issue(const OverlayBorder& border) const;
            void SetMarkerPosition(const MarkerRef& marker, double position);
            void Save();
            void Reset();
            void TogglePreview();
            void ToggleEdgeMap();
            void ToggleStop();
            void ToggleTrail();
            void AddTrailDots(size_t index, POINT client);
            void UpdateTrail(size_t index);
            void ClearTrail();
            std::optional<Edge> WrapExit(size_t fromIndex, POINT from, size_t toIndex, POINT to) const;
            void AddWrapLines(size_t fromIndex, POINT from, Edge exit);
            Engine CurrentEngine() const;
            void UpdatePreview();
            void SavePreferences() const;

            // Geometry, in client coordinates of the window of the marker's monitor.
            const RECT& Rect(size_t monitor) const;
            size_t MonitorOf(const MarkerRef& marker) const;
            Edge EdgeOf(const MarkerRef& marker) const;
            double FractionOf(const MarkerRef& marker) const;
            double PositionOf(const MarkerRef& marker) const;
            D2D1_RECT_F MarkerRect(const MarkerRef& marker, double fraction) const;
            size_t ToggleMonitor(const OverlayBorder& border) const;
            bool HasSwitch(const OverlayBorder& border, size_t monitor) const;
            D2D1_RECT_F SwitchRect(const OverlayBorder& border, size_t monitor) const;
            D2D1_RECT_F NaturalSwitchRect(const OverlayBorder& border, size_t monitor) const;
            void LayOutSwitches();
            bool MoveSwitchesFromPanel();
            void MeasurePanelText();
            D2D1_RECT_F PanelRect() const;
            bool PrimaryHasBorder(Edge edge) const;
            float SwitchRow(Edge edge) const;
            std::vector<MarkerRef> Markers(bool enabledOnly) const;
            std::optional<MarkerRef> HitTestMarker(size_t index, POINT client) const;

            // Input.
            void OnButtonDown(size_t index, POINT client);
            void OnDrag(size_t index, POINT client);
            void OnKey(WPARAM key, bool repeat);
            void MoveFocus(bool backward);
            void PressFocused();
            bool IsFocused(const FocusStop& stop) const;
            void ToggleBorder(size_t borderIndex);
            void RunAction(Action action);

            // Animation.
            void StartAnimation();
            void StopAnimation();
            std::optional<Ghost> CurrentGhost() const;

            // Rendering.
            bool CreateTarget(OverlayWindow& window);
            winrt::com_ptr<IDWriteTextFormat> MakeFormat(float size, DWRITE_FONT_WEIGHT weight, DWRITE_TEXT_ALIGNMENT alignment) const;
            void Render(size_t index);
            void DrawBorder(size_t index, size_t borderIndex);
            void DrawSwitch(size_t index, size_t borderIndex);
            void DrawMarker(OverlayWindow& window, const MarkerRef& marker, double fraction, float alpha);
            void DrawPanel(OverlayWindow& window);
            void DrawOtherMonitorEnds(OverlayWindow& window, const OverlayBorder& border, bool onB);
            void DrawEdgeMap(size_t index);
            void DrawGlueTube(OverlayWindow& window, D2D1_POINT_2F center, float size, UINT32 bodyColor, UINT32 detailColor);
            void DrawFocus(OverlayWindow& window, const D2D1_RECT_F& rect, float radius);
            void DrawTrail(OverlayWindow& window);
            winrt::com_ptr<ID2D1PathGeometry> Polygon(std::initializer_list<D2D1_POINT_2F> points) const;
            void DrawLabel(OverlayWindow& window, const std::wstring& text, IDWriteTextFormat* format, const D2D1_RECT_F& rect, UINT32 color, bool clip = true);
            const std::wstring& PanelMessage() const;
            const std::wstring& KeysText() const;
            std::wstring ArrangeKeyHints(const OverlayWindow& window, float width) const;
            void InvalidateBorder(size_t borderIndex) const;
            void InvalidateAll() const;
            const std::wstring& IssueText(CalibrationIssue issue) const;

            void DestroyWindows() noexcept;
            std::optional<size_t> IndexOf(HWND hwnd) const;
            size_t PrimaryIndex() const;

            const CalibrationOverlayCallbacks& m_callbacks;
            EngineOptions m_engineOptions;
            Strings m_strings;
            winrt::com_ptr<ID2D1Factory> m_d2d;
            winrt::com_ptr<IDWriteFactory> m_dwrite;
            winrt::com_ptr<ID2D1StrokeStyle> m_dashStyle;
            std::vector<OverlayWindow> m_windows;
            std::vector<OverlayBorder> m_borders;

            std::optional<MarkerRef> m_selected;
            std::optional<FocusStop> m_focused; // A switch or button, which Space presses; a focused line is m_selected.
            std::optional<MarkerRef> m_dragged;
            double m_grabOffset = 0.0;

            bool m_animating = false;
            ULONGLONG m_animationStart = 0;
            std::optional<ULONGLONG> m_now; // A snapshot's clock; otherwise the tick count.

            bool m_finished = false;
            bool m_saved = false;
            bool m_saveFailed = false;
            bool m_storedUnreadable = false; // See CreateBorders.
            bool m_previewing = false;
            bool m_showEdgeMap = false;
            bool m_showTrail = false;
            std::optional<std::pair<size_t, TrailDot>> m_trailEnd; // The monitor and the newest dot the trail continues from.
            std::optional<std::pair<size_t, POINT>> m_lastPointer; // The monitor and screen position of the last move seen.
            bool m_monitorsUnknown = false; // Windows cannot tell the monitors' device paths, so nothing can be calibrated.
            bool m_compactPanel = false; // See LayOutSwitches.
            std::wstring m_keysText; // The key hints arranged in rows for the panel's width; see MeasurePanelText.

            // How much more room than usual each panel text needs, in pixels.
            struct
            {
                float instructions = 0.0f;
                float keys = 0.0f;
                float legend = 0.0f;
            } m_textExtra;
        };

        bool Session::Open()
        {
            if (!Prepare(EnumerateMonitors(), LoadOverlayPreferences(), [] { return LoadCalibrations(); }))
            {
                return false;
            }

            WNDCLASSEXW windowClass{};
            windowClass.cbSize = sizeof(windowClass);
            windowClass.lpfnWndProc = WindowProc;
            windowClass.hInstance = ModuleInstance();
            windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
            windowClass.lpszClassName = windowClassName;
            RegisterClassExW(&windowClass);

            for (auto& window : m_windows)
            {
                const RECT& rect = window.details.monitor.rect;
                window.hwnd = CreateWindowExW(
                    WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_LAYERED,
                    windowClassName,
                    m_strings.title.c_str(),
                    WS_POPUP,
                    rect.left,
                    rect.top,
                    rect.right - rect.left,
                    rect.bottom - rect.top,
                    nullptr,
                    nullptr,
                    ModuleInstance(),
                    this);
                if (!window.hwnd)
                {
                    Logger::error(L"Failed to create a calibration overlay window. Error: {}", GetLastError());
                    return false;
                }

                SetLayeredWindowAttributes(window.hwnd, 0, windowOpacity, LWA_ALPHA);
                window.scale = GetDpiForWindow(window.hwnd) / static_cast<float>(USER_DEFAULT_SCREEN_DPI);
                CreateFormats(window);
            }

            LayOutSwitches();
            for (const auto& window : m_windows)
            {
                ShowWindow(window.hwnd, SW_SHOWNA);
            }

            BringToFront();
            if (!m_borders.empty() && m_borders.front().enabled)
            {
                StartAnimation();
            }

            // Suspend first, then try the lines right away when that is the user's preference.
            m_callbacks.opened();
            UpdatePreview();
            return true;
        }

        // Everything that does not need windows; Open and Snapshot pass in what they get from the system or the caller.
        bool Session::Prepare(std::vector<MonitorDetails> monitors, const OverlayPreferences& preferences, const std::function<std::optional<std::vector<BorderCalibration>>()>& loadSaved)
        {
            if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, m_d2d.put())) ||
                FAILED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), reinterpret_cast<IUnknown**>(m_dwrite.put()))))
            {
                Logger::error("Failed to create Direct2D or DirectWrite factories for the calibration overlay.");
                return false;
            }

            const std::array<float, 2> dashes{ 4.0f, 3.0f };
            const auto dashProperties = D2D1::StrokeStyleProperties(D2D1_CAP_STYLE_FLAT, D2D1_CAP_STYLE_FLAT, D2D1_CAP_STYLE_FLAT, D2D1_LINE_JOIN_MITER, 10.0f, D2D1_DASH_STYLE_CUSTOM);
            m_d2d->CreateStrokeStyle(dashProperties, dashes.data(), static_cast<UINT32>(dashes.size()), m_dashStyle.put());
            m_previewing = preferences.tryWhileAdjusting;
            m_showEdgeMap = preferences.showEdgeMap;
            m_showTrail = preferences.showTrail;

            for (auto& details : monitors)
            {
                m_windows.push_back({ std::move(details) });
            }

            if (m_windows.empty())
            {
                return false;
            }

            // Calibrations name monitors by device path. Without one (for example in a remote session), nothing saved
            // could ever apply, so the panel only says so.
            m_monitorsUnknown = std::any_of(m_windows.begin(), m_windows.end(), [](const OverlayWindow& w) { return !w.details.identified; });
            if (!m_monitorsUnknown)
            {
                CreateBorders(loadSaved());
            }

            return true;
        }

        void Session::CreateFormats(OverlayWindow& window)
        {
            window.captionFormat = MakeFormat(30.0f * window.scale, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_TEXT_ALIGNMENT_CENTER);
            window.titleFormat = MakeFormat(22.0f * window.scale, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_TEXT_ALIGNMENT_LEADING);
            window.textFormat = MakeFormat(15.0f * window.scale, DWRITE_FONT_WEIGHT_NORMAL, DWRITE_TEXT_ALIGNMENT_LEADING);
            window.centeredFormat = MakeFormat(14.0f * window.scale, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_TEXT_ALIGNMENT_CENTER);
            window.pillFormat = MakeFormat(14.0f * window.scale, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_TEXT_ALIGNMENT_CENTER);
            if (window.pillFormat)
            {
                const DWRITE_TRIMMING trimming{ DWRITE_TRIMMING_GRANULARITY_CHARACTER, 0, 0 };
                window.pillFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP);
                window.pillFormat->SetTrimming(&trimming, nullptr);
            }

            window.issueBelowFormat = MakeFormat(14.0f * window.scale, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_TEXT_ALIGNMENT_CENTER);
            window.issueAboveFormat = MakeFormat(14.0f * window.scale, DWRITE_FONT_WEIGHT_SEMI_BOLD, DWRITE_TEXT_ALIGNMENT_CENTER);
            if (window.issueBelowFormat && window.issueAboveFormat)
            {
                window.issueBelowFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR);
                window.issueAboveFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_FAR);
            }
        }

        // The overlay as Open would show it, drawn into one bitmap per monitor: no windows, timers or callbacks.
        std::vector<winrt::com_ptr<IWICBitmap>> Session::Snapshot(IWICImagingFactory* wic, const SnapshotRequest& request)
        {
            std::vector<MonitorDetails> monitors;
            for (const auto& monitor : request.monitors)
            {
                monitors.push_back(monitor.details);
            }

            if (!wic || !Prepare(std::move(monitors), request.preferences, [&request] { return std::optional{ request.saved }; }))
            {
                return {};
            }

            for (size_t i = 0; i < m_windows.size(); ++i)
            {
                auto& window = m_windows[i];
                const RECT& rect = window.details.monitor.rect;
                window.scale = request.monitors[i].scale;
                if (FAILED(wic->CreateBitmap(rect.right - rect.left, rect.bottom - rect.top, GUID_WICPixelFormat32bppPBGRA, WICBitmapCacheOnLoad, window.bitmap.put())))
                {
                    return {};
                }

                CreateFormats(window);
            }

            LayOutSwitches();
            if (request.movePointer)
            {
                // Moves land on the monitor under them, as mouse moves on its window would.
                class Pointer final : public SnapshotPointer
                {
                public:
                    explicit Pointer(Session& session) :
                        m_session(session)
                    {
                    }

                    Engine CurrentEngine() const override
                    {
                        return m_session.CurrentEngine();
                    }

                    void Move(POINT screen, ULONGLONG time) override
                    {
                        m_session.m_now = time;
                        for (size_t i = 0; i < m_session.m_windows.size(); ++i)
                        {
                            const RECT& rect = m_session.Rect(i);
                            if (PtInRect(&rect, screen))
                            {
                                m_session.AddTrailDots(i, { screen.x - rect.left, screen.y - rect.top });
                                return;
                            }
                        }
                    }

                private:
                    Session& m_session;
                };

                Pointer pointer{ *this };
                request.movePointer(pointer);
            }

            if (request.time != 0)
            {
                m_now = request.time;
            }

            std::vector<winrt::com_ptr<IWICBitmap>> bitmaps;
            for (size_t i = 0; i < m_windows.size(); ++i)
            {
                Render(i);
                if (!m_windows[i].target)
                {
                    return {};
                }

                bitmaps.push_back(m_windows[i].bitmap);
            }

            return bitmaps;
        }

        ULONGLONG Session::Now() const
        {
            return m_now ? *m_now : GetTickCount64();
        }

        void Session::Finish(bool saved)
        {
            if (m_finished)
            {
                return;
            }

            m_finished = true;
            m_saved = saved;
            StopAnimation();
            DestroyWindows();
            PostQuitMessage(0);
        }

        void Session::BringToFront()
        {
            if (!m_windows.empty() && m_windows[PrimaryIndex()].hwnd)
            {
                BringToForeground(m_windows[PrimaryIndex()].hwnd);
            }
        }

        void Session::DestroyWindows() noexcept
        {
            for (auto& window : m_windows)
            {
                if (window.hwnd)
                {
                    const HWND hwnd = window.hwnd;
                    window.hwnd = nullptr;
                    DestroyWindow(hwnd);
                }
            }
        }

        std::optional<size_t> Session::IndexOf(HWND hwnd) const
        {
            for (size_t i = 0; i < m_windows.size(); ++i)
            {
                if (m_windows[i].hwnd == hwnd)
                {
                    return i;
                }
            }

            return std::nullopt;
        }

        size_t Session::PrimaryIndex() const
        {
            const auto primary = std::find_if(m_windows.begin(), m_windows.end(), [](const OverlayWindow& w) { return w.details.primary; });
            return primary == m_windows.end() ? 0 : static_cast<size_t>(primary - m_windows.begin());
        }

        void Session::CreateBorders(std::optional<std::vector<BorderCalibration>> saved)
        {
            std::vector<RECT> rects;
            for (const auto& window : m_windows)
            {
                rects.push_back(window.details.monitor.rect);
            }

            // While another program holds the file, the borders show predictions; saving them would replace the stored
            // lines, so Save stays blocked, and the panel says so from the start.
            m_storedUnreadable = !saved;
            if (!saved)
            {
                saved.emplace();
            }

            const auto add = [&](const SharedBorder& shared, bool glue) {
                OverlayBorder border;
                border.a = shared.monitorA;
                border.b = shared.monitorB;
                border.edgeOfA = shared.edgeOfA;
                border.sharedStart = shared.start;
                border.sharedEnd = shared.end;
                border.glue = glue;
                border.savedAsGlue = glue;
                border.enabled = !glue;
                border.color = borderColors[m_borders.size() % borderColors.size()];
                if (!ApplySaved(border, *saved))
                {
                    border.pairs = Predict(border);
                }

                m_borders.push_back(border);
            };

            // Shared borders first: the hint animation plays on the first border.
            for (const auto& shared : FindSharedBorders(rects))
            {
                add(shared, false);
            }

            for (const auto& candidate : FindGlueCandidates(rects))
            {
                add(candidate, true);
            }
        }

        bool Session::ApplySaved(OverlayBorder& border, const std::vector<BorderCalibration>& saved) const
        {
            for (const auto& calibration : saved)
            {
                const auto match = MatchSaved(calibration, border);
                if (match == SavedMatch::None)
                {
                    continue;
                }

                // An unglued pair is saved switched off, which says nothing about a border these monitors now share.
                border.enabled = calibration.enabled || (calibration.glue && !border.glue);
                for (size_t i = 0; i < border.pairs.size(); ++i)
                {
                    const auto& pair = calibration.pairs[i];
                    border.pairs[i] = match == SavedMatch::Same ? pair : MarkerPair{ pair.b, pair.a };
                }

                border.secondPairMoved = true;
                border.keep = true;
                border.savedAsGlue = calibration.glue;
                return true;
            }

            return false;
        }

        // A calibration is for a border when it joins the same monitors across the same edge, with each monitor in the
        // orientation it has now: rotating a monitor makes its stored fractions meaningless.
        SavedMatch Session::MatchSaved(const BorderCalibration& calibration, const OverlayBorder& border) const
        {
            const auto& idA = m_windows[border.a].details.monitor.id;
            const auto& idB = m_windows[border.b].details.monitor.id;
            const bool landscapeA = IsLandscape(Rect(border.a));
            const bool landscapeB = IsLandscape(Rect(border.b));
            if (calibration.monitorA == idA && calibration.monitorB == idB && calibration.edgeOfA == border.edgeOfA &&
                calibration.landscapeA == landscapeA && calibration.landscapeB == landscapeB)
            {
                return SavedMatch::Same;
            }

            if (calibration.monitorA == idB && calibration.monitorB == idA && calibration.edgeOfA == Opposite(border.edgeOfA) &&
                calibration.landscapeA == landscapeB && calibration.landscapeB == landscapeA)
            {
                return SavedMatch::Swapped;
            }

            return SavedMatch::None;
        }

        double Session::PredictedScale(const OverlayBorder& border) const
        {
            const double pitchA = m_windows[border.a].details.mmPerPixel;
            const double pitchB = m_windows[border.b].details.mmPerPixel;
            return pitchA > 0.0 && pitchB > 0.0 ? pitchA / pitchB : 1.0;
        }

        std::array<MarkerPair, 2> Session::Predict(const OverlayBorder& border) const
        {
            const RECT& a = Rect(border.a);
            const RECT& b = Rect(border.b);
            const Edge edgeOfA = border.edgeOfA;
            const Edge edgeOfB = Opposite(edgeOfA);

            // Assume Windows has the monitors right at the middle of the shared segment.
            const double middle = (static_cast<double>(border.sharedStart) + border.sharedEnd) / 2.0;
            const BorderMap map{ middle, middle, PredictedScale(border) };

            // Spread the pairs over the span that both monitors physically cover.
            const double startB = EdgeStart(b, edgeOfB);
            const double start = std::max(static_cast<double>(EdgeStart(a, edgeOfA)), map.ToA(startB));
            const double end = std::min(static_cast<double>(EdgeStart(a, edgeOfA)) + EdgeLength(a, edgeOfA), map.ToA(startB + EdgeLength(b, edgeOfB)));

            std::array<MarkerPair, 2> pairs{};
            const std::array<double, 2> spots{ 0.25, 0.75 };
            for (size_t i = 0; i < pairs.size(); ++i)
            {
                const double uA = start + (end - start) * spots[i];
                pairs[i] = { MarkerFraction(a, edgeOfA, uA), std::clamp(MarkerFraction(b, edgeOfB, map.ToB(uA)), 0.0, 1.0) };
            }

            return pairs;
        }

        void Session::PredictSecondPair(OverlayBorder& border) const
        {
            const RECT& a = Rect(border.a);
            const RECT& b = Rect(border.b);
            const Edge edgeOfA = border.edgeOfA;
            const Edge edgeOfB = Opposite(edgeOfA);
            const double a0 = MarkerPosition(a, edgeOfA, border.pairs[0].a);
            const double b0 = MarkerPosition(b, edgeOfB, border.pairs[0].b);
            const double a1 = MarkerPosition(a, edgeOfA, border.pairs[1].a);
            const double b1 = b0 + PredictedScale(border) * (a1 - a0);
            border.pairs[1].b = std::clamp(MarkerFraction(b, edgeOfB, b1), 0.0, 1.0);
        }

        BorderCalibration Session::ToCalibration(const OverlayBorder& border) const
        {
            BorderCalibration calibration;
            calibration.monitorA = m_windows[border.a].details.monitor.id;
            calibration.monitorB = m_windows[border.b].details.monitor.id;
            calibration.edgeOfA = border.edgeOfA;
            calibration.enabled = border.enabled;
            calibration.pairs = border.pairs;
            calibration.landscapeA = IsLandscape(Rect(border.a));
            calibration.landscapeB = IsLandscape(Rect(border.b));
            calibration.glue = border.savedAsGlue;
            return calibration;
        }

        CalibrationIssue Session::Issue(const OverlayBorder& border) const
        {
            return Validate(ToCalibration(border), Rect(border.a), Rect(border.b));
        }

        void Session::SetMarkerPosition(const MarkerRef& marker, double position)
        {
            const RECT& rect = Rect(MonitorOf(marker));
            const Edge edge = EdgeOf(marker);
            const double start = EdgeStart(rect, edge);
            const double fraction = MarkerFraction(rect, edge, std::clamp(position, start + 0.5, start + EdgeLength(rect, edge) - 0.5));

            auto& border = m_borders[marker.border];
            auto& pair = border.pairs[marker.pair];
            (marker.onB ? pair.b : pair.a) = fraction;
            ClearTrail();
            border.keep = true;
            if (marker.pair == 1)
            {
                border.secondPairMoved = true;
            }
            else if (!border.secondPairMoved)
            {
                PredictSecondPair(border);
            }

            InvalidateBorder(marker.border);
            UpdatePreview();
        }

        void Session::Save()
        {
            if (m_storedUnreadable)
            {
                return;
            }

            // Keep what the borders on screen do not replace: monitor pairs that are not side by side right now, and pairs
            // calibrated across another edge or with a monitor rotated, for when they are arranged that way again.
            auto stored = LoadCalibrationsForSave();
            if (!stored)
            {
                // The file cannot be read nor backed up; replacing it could lose calibrations for good.
                m_saveFailed = true;
                InvalidateAll();
                return;
            }

            std::vector<BorderCalibration> calibrations;
            for (auto& calibration : *stored)
            {
                const bool replaced = std::any_of(m_borders.begin(), m_borders.end(), [&](const OverlayBorder& border) {
                    return MatchSaved(calibration, border) != SavedMatch::None;
                });
                if (!replaced)
                {
                    calibrations.push_back(std::move(calibration));
                }
            }

            int savedCount = 0;
            int enabledCount = 0;
            int invalidCount = 0;
            for (const auto& border : m_borders)
            {
                // An unglued pair whose lines were never stored, glued or moved has nothing worth keeping.
                if (border.glue && !border.enabled && !border.keep)
                {
                    continue;
                }

                calibrations.push_back(ToCalibration(border));
                ++savedCount;
                enabledCount += border.enabled ? 1 : 0;
                invalidCount += (border.enabled && !IsUsable(Issue(border))) ? 1 : 0;
            }

            if (!SaveCalibrations(calibrations))
            {
                // Stay open so the alignment is not lost; the panel tells the user to try again.
                m_saveFailed = true;
                InvalidateAll();
                return;
            }

            Trace::CalibrationSaved(savedCount, enabledCount, invalidCount);
            Finish(true);
        }

        void Session::Reset()
        {
            ClearTrail();
            for (auto& border : m_borders)
            {
                border.enabled = !border.glue;
                border.keep = false;
                border.savedAsGlue = border.glue;
                border.secondPairMoved = false;
                border.pairs = Predict(border);
            }

            // Ungluing hides the lines of a pair.
            if (m_selected && !m_borders[m_selected->border].enabled)
            {
                m_selected.reset();
            }

            InvalidateAll();
            UpdatePreview();
        }

        void Session::SavePreferences() const
        {
            SaveOverlayPreferences({ m_previewing, m_showEdgeMap, m_showTrail });
        }

        void Session::ToggleEdgeMap()
        {
            m_showEdgeMap = !m_showEdgeMap;
            SavePreferences();
            InvalidateAll();
        }

        // The same setting as in Settings, so it applies at once and is not undone by Cancel. The edge map shows its effect.
        void Session::ToggleStop()
        {
            m_engineOptions.stopWhereNothingIsAcross = !m_engineOptions.stopWhereNothingIsAcross;
            m_callbacks.stopChanged(m_engineOptions.stopWhereNothingIsAcross);
            ClearTrail();
            InvalidateAll();
        }

        void Session::ToggleTrail()
        {
            m_showTrail = !m_showTrail;
            SavePreferences();
            ClearTrail();
            InvalidateAll();
        }

        // Dots every few pixels along the path, with times spread over the move, so the trail fades smoothly. A move
        // onto another monitor, or a jump (CursorWrap), starts a new piece of trail.
        void Session::AddTrailDots(size_t index, POINT client)
        {
            if (!m_showTrail || m_dragged)
            {
                return;
            }

            auto& window = m_windows[index];
            const ULONGLONG now = Now();
            const D2D1_POINT_2F point{ static_cast<float>(client.x), static_cast<float>(client.y) };
            const float spacing = trailSpacing * window.scale;
            const RECT& rect = window.details.monitor.rect;
            const POINT screen{ rect.left + client.x, rect.top + client.y };
            if (m_lastPointer)
            {
                if (const auto exit = WrapExit(m_lastPointer->first, m_lastPointer->second, index, screen))
                {
                    AddWrapLines(m_lastPointer->first, m_lastPointer->second, *exit);
                }
            }

            m_lastPointer = { index, screen };
            bool continued = false;
            if (m_trailEnd && m_trailEnd->first == index)
            {
                const TrailDot from = m_trailEnd->second;
                const float dx = point.x - from.point.x;
                const float dy = point.y - from.point.y;
                const float distance = std::hypot(dx, dy);
                if (distance < spacing)
                {
                    return;
                }

                // Per axis, so a wrap between the top and bottom of a very wide monitor counts as a jump too.
                const bool jumped = std::abs(dx) > 0.5f * static_cast<float>(rect.right - rect.left) ||
                                    std::abs(dy) > 0.5f * static_cast<float>(rect.bottom - rect.top);
                if (!jumped)
                {
                    // After a pause, the new dots all count from now, so the start of the move does not fade at once.
                    const ULONGLONG start = now - std::min(now - from.time, ULONGLONG{ animationFrameMs } * 4);
                    const int steps = static_cast<int>(distance / spacing);
                    for (int step = 1; step <= steps; ++step)
                    {
                        const float part = static_cast<float>(step) * spacing / distance;
                        window.trail.push_back({ { from.point.x + dx * part, from.point.y + dy * part }, start + static_cast<ULONGLONG>(static_cast<double>(now - start) * part) });
                    }

                    continued = true;
                }
            }

            if (!continued)
            {
                window.trail.push_back({ point, now });
            }

            m_trailEnd = { index, window.trail.back() };
            while (window.trail.size() > trailMaxDots)
            {
                window.trail.pop_front();
            }

            // A frame timer runs only while this monitor has dots left to fade.
            if (!window.trailTimerRunning && window.hwnd)
            {
                window.trailTimerRunning = SetTimer(window.hwnd, trailTimer, animationFrameMs, nullptr) != 0;
            }
        }

        void Session::UpdateTrail(size_t index)
        {
            auto& window = m_windows[index];
            const ULONGLONG now = Now();
            while (!window.trail.empty() && now - window.trail.front().time >= trailLifetimeMs)
            {
                window.trail.pop_front();
            }

            while (!window.wrapLines.empty() && now - window.wrapLines.front().time >= wrapLineLifetimeMs)
            {
                window.wrapLines.pop_front();
            }

            InvalidateRect(window.hwnd, nullptr, FALSE);
            if (window.trail.empty() && window.wrapLines.empty())
            {
                KillTimer(window.hwnd, trailTimer);
                window.trailTimerRunning = false;
            }
        }

        // A trail made with other lines or settings would mislead, so any change that affects crossings clears it at once.
        void Session::ClearTrail()
        {
            m_trailEnd.reset();
            for (auto& window : m_windows)
            {
                if (window.trail.empty() && window.wrapLines.empty())
                {
                    continue;
                }

                window.trail.clear();
                window.wrapLines.clear();
                if (window.hwnd)
                {
                    KillTimer(window.hwnd, trailTimer);
                    InvalidateRect(window.hwnd, nullptr, FALSE);
                }

                window.trailTimerRunning = false;
            }
        }

        // CursorWrap moves the pointer from one side of the desktop to the opposite side of the same or another monitor,
        // against the direction of the move, by more than half a monitor. A crossing between monitors keeps the direction.
        // Mouse moves arrive merged while the overlay draws, so after a fast wrap the first one seen can already be well
        // inside the monitor; the outer quarter on the landing side counts.
        std::optional<Edge> Session::WrapExit(size_t fromIndex, POINT from, size_t toIndex, POINT to) const
        {
            const RECT& source = Rect(fromIndex);
            const RECT& target = Rect(toIndex);
            const LONG halfWidth = (source.right - source.left) / 2;
            const LONG halfHeight = (source.bottom - source.top) / 2;
            const LONG landingWidth = (target.right - target.left) / 4;
            const LONG landingHeight = (target.bottom - target.top) / 4;
            if (to.x < from.x - halfWidth && to.x < target.left + landingWidth)
            {
                return Edge::Right;
            }

            if (to.x > from.x + halfWidth && to.x >= target.right - landingWidth)
            {
                return Edge::Left;
            }

            if (to.y < from.y - halfHeight && to.y < target.top + landingHeight)
            {
                return Edge::Bottom;
            }

            if (to.y > from.y + halfHeight && to.y >= target.bottom - landingHeight)
            {
                return Edge::Top;
            }

            return std::nullopt;
        }

        // A line on every monitor that the calibration links to the one left, through borders across the wrap direction,
        // where the same physical height (or horizontal position) lies. If the wrap kept it, the pointer lands on the line.
        void Session::AddWrapLines(size_t fromIndex, POINT from, Edge exit)
        {
            const bool horizontal = !IsHorizontal(exit);
            const int along = horizontal ? from.y : from.x;
            const auto engine = CurrentEngine();
            const ULONGLONG now = Now();
            for (size_t i = 0; i < m_windows.size(); ++i)
            {
                auto& window = m_windows[i];
                const RECT& rect = Rect(i);
                const auto mapped = engine.MapJump(Rect(fromIndex), exit, along, rect);
                const LONG start = horizontal ? rect.top : rect.left;
                const LONG end = horizontal ? rect.bottom : rect.right;
                if (!mapped || *mapped < start || *mapped >= end)
                {
                    continue;
                }

                window.wrapLines.push_back({ horizontal, static_cast<float>(*mapped - start) + 0.5f, now });
                if (!window.trailTimerRunning && window.hwnd)
                {
                    window.trailTimerRunning = SetTimer(window.hwnd, trailTimer, animationFrameMs, nullptr) != 0;
                }
            }
        }

        // The engine for the lines as currently placed, as Try while adjusting and the edge map use them.
        Engine Session::CurrentEngine() const
        {
            std::vector<Monitor> monitors;
            for (const auto& window : m_windows)
            {
                monitors.push_back(window.details.monitor);
            }

            std::vector<BorderCalibration> calibrations;
            for (const auto& border : m_borders)
            {
                if (!border.glue || border.enabled)
                {
                    calibrations.push_back(ToCalibration(border));
                }
            }

            Engine engine;
            engine.Build(monitors, calibrations, m_engineOptions);
            return engine;
        }

        void Session::TogglePreview()
        {
            m_previewing = !m_previewing;
            SavePreferences();
            ClearTrail();
            if (m_previewing)
            {
                UpdatePreview();
            }
            else
            {
                m_callbacks.preview(std::nullopt);
            }

            InvalidateAll();
        }

        // While previewing, the hook remaps with the lines as currently placed. It pauses while a line is dragged,
        // so the dragged line does not move the cursor around, and catches up when the drag ends.
        void Session::UpdatePreview()
        {
            if (!m_previewing || m_dragged || m_finished)
            {
                return;
            }

            std::vector<BorderCalibration> calibrations;
            for (const auto& border : m_borders)
            {
                calibrations.push_back(ToCalibration(border));
            }

            m_callbacks.preview(std::move(calibrations));
        }

        const RECT& Session::Rect(size_t monitor) const
        {
            return m_windows[monitor].details.monitor.rect;
        }

        size_t Session::MonitorOf(const MarkerRef& marker) const
        {
            const auto& border = m_borders[marker.border];
            return marker.onB ? border.b : border.a;
        }

        Edge Session::EdgeOf(const MarkerRef& marker) const
        {
            const auto& border = m_borders[marker.border];
            return marker.onB ? Opposite(border.edgeOfA) : border.edgeOfA;
        }

        double Session::FractionOf(const MarkerRef& marker) const
        {
            const auto& pair = m_borders[marker.border].pairs[marker.pair];
            return marker.onB ? pair.b : pair.a;
        }

        double Session::PositionOf(const MarkerRef& marker) const
        {
            return MarkerPosition(Rect(MonitorOf(marker)), EdgeOf(marker), FractionOf(marker));
        }

        D2D1_RECT_F Session::MarkerRect(const MarkerRef& marker, double fraction) const
        {
            const auto& window = m_windows[MonitorOf(marker)];
            const RECT& rect = window.details.monitor.rect;
            const Edge edge = EdgeOf(marker);
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);
            const float half = markerThickness * window.scale / 2.0f;
            const float length = markerLengthRatio * (IsHorizontal(edge) ? height : width);
            const float along = static_cast<float>(MarkerPosition(rect, edge, fraction) - EdgeStart(rect, edge));

            switch (edge)
            {
            case Edge::Top:
                return { along - half, 0.0f, along + half, length };
            case Edge::Bottom:
                return { along - half, height - length, along + half, height };
            case Edge::Left:
                return { 0.0f, along - half, length, along + half };
            default:
                return { width - length, along - half, width, along + half };
            }
        }

        // The on/off switch sits on the primary monitor when the border touches it, otherwise on monitor A.
        size_t Session::ToggleMonitor(const OverlayBorder& border) const
        {
            return m_windows[border.b].details.primary ? border.b : border.a;
        }

        // A glued pair is one link between two monitors, so both of them carry its switch, each naming the other.
        bool Session::HasSwitch(const OverlayBorder& border, size_t monitor) const
        {
            return border.glue ? (monitor == border.a || monitor == border.b) : monitor == ToggleMonitor(border);
        }

        // Laid out once when the overlay opens (see LayOutSwitches).
        D2D1_RECT_F Session::SwitchRect(const OverlayBorder& border, size_t monitor) const
        {
            return border.switches[monitor == border.b ? 1 : 0];
        }

        // A pill near the middle of the border, inside the given monitor's window.
        D2D1_RECT_F Session::NaturalSwitchRect(const OverlayBorder& border, size_t monitor) const
        {
            const auto& window = m_windows[monitor];
            const RECT& rect = window.details.monitor.rect;
            const Edge edge = monitor == border.a ? border.edgeOfA : Opposite(border.edgeOfA);
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);
            const float w = (border.glue ? glueToggleWidth : toggleWidth) * window.scale;
            const float h = toggleHeight * window.scale;
            const float inset = markerLengthRatio * (IsHorizontal(edge) ? height : width) + switchGap * window.scale;
            const float middle = (static_cast<float>(border.sharedStart) + border.sharedEnd) / 2.0f - EdgeStart(rect, edge);

            float x = 0.0f;
            float y = 0.0f;
            switch (edge)
            {
            case Edge::Top:
                x = middle;
                y = inset + h / 2.0f;
                break;
            case Edge::Bottom:
                x = middle;
                y = height - inset - h / 2.0f;
                break;
            case Edge::Left:
                x = inset + w / 2.0f;
                y = middle;
                break;
            default:
                x = width - inset - w / 2.0f;
                y = middle;
                break;
            }

            // Not std::clamp: on a monitor narrower than the switch, its range would be empty. On a side edge the issue
            // text goes below the switch, so it needs room there too.
            const float below = IsHorizontal(edge) ? 0.0f : (issueGap + issueHeight) * window.scale;
            x = std::max(w / 2.0f, std::min(x, width - w / 2.0f));
            y = std::max(h / 2.0f, std::min(y, height - h / 2.0f - below));
            return { x - w / 2.0f, y - h / 2.0f, x + w / 2.0f, y + h / 2.0f };
        }

        // Switch positions depend only on the monitor layout, so they are laid out once when the overlay opens. When the
        // full panel leaves a switch on the primary monitor covered, the panel turns compact and they are laid out again.
        void Session::LayOutSwitches()
        {
            MeasurePanelText();
            const RECT& primary = Rect(PrimaryIndex());
            for (const bool compact : { false, true })
            {
                m_compactPanel = compact;
                for (auto& border : m_borders)
                {
                    for (const size_t monitor : { border.a, border.b })
                    {
                        if (HasSwitch(border, monitor))
                        {
                            border.switches[monitor == border.b ? 1 : 0] = NaturalSwitchRect(border, monitor);
                        }
                    }
                }

                // A full panel taller than the monitor gives way too, unless it only has the message that there are no
                // borders, which the compact panel leaves out.
                const bool panelFits = m_borders.empty() || PanelRect().bottom <= static_cast<float>(primary.bottom - primary.top);
                if (MoveSwitchesFromPanel() && panelFits)
                {
                    return;
                }
            }
        }

        // Long translations or a narrow panel can need more lines than the usual room; the panel grows by the difference.
        void Session::MeasurePanelText()
        {
            m_textExtra = {};
            const auto& window = m_windows[PrimaryIndex()];
            const float s = window.scale;
            const auto panel = PanelRect();
            const float width = std::max(1.0f, panel.right - panel.left - 2.0f * panelPadding * s);
            const auto extra = [&](const std::wstring& text, float room) {
                winrt::com_ptr<IDWriteTextLayout> layout;
                DWRITE_TEXT_METRICS metrics{};
                if (!window.textFormat ||
                    FAILED(m_dwrite->CreateTextLayout(text.c_str(), static_cast<UINT32>(text.size()), window.textFormat.get(), width, room, layout.put())) ||
                    FAILED(layout->GetMetrics(&metrics)))
                {
                    return 0.0f;
                }

                return std::max(0.0f, std::ceil(metrics.height) - room);
            };

            m_textExtra.instructions = extra(PanelMessage(), instructionsHeight * s);

            // Without borders the panel shows neither the key hints nor the legend.
            if (!m_borders.empty())
            {
                m_keysText = ArrangeKeyHints(window, width);
                m_textExtra.keys = std::max({ extra(m_keysText, keysHeight * s), extra(m_strings.saveFailed, keysHeight * s), extra(m_strings.saveBlocked, keysHeight * s) });
                m_textExtra.legend = extra(m_strings.mapLegend, legendHeight * s);
            }
        }

        // The instructions, or why there is nothing to calibrate.
        const std::wstring& Session::PanelMessage() const
        {
            if (!m_borders.empty())
            {
                return m_strings.instructions;
            }

            return m_monitorsUnknown ? m_strings.monitorsUnknown : m_strings.noBorders;
        }

        // The key hints, or why saving cannot work or did not.
        const std::wstring& Session::KeysText() const
        {
            if (m_storedUnreadable)
            {
                return m_strings.saveBlocked;
            }

            return m_saveFailed ? m_strings.saveFailed : m_keysText;
        }

        // The key hints in rows, divided by dots: a hint that does not fit on the row starts the next one, so no key is
        // separated from its description.
        std::wstring Session::ArrangeKeyHints(const OverlayWindow& window, float width) const
        {
            constexpr wchar_t noBreakSpace = L' ';
            const std::wstring separator{ noBreakSpace, noBreakSpace, L'·', noBreakSpace, noBreakSpace };
            const auto measure = [&](const std::wstring& text) {
                constexpr float unbounded = 100000.0f;
                winrt::com_ptr<IDWriteTextLayout> layout;
                DWRITE_TEXT_METRICS metrics{};
                if (!window.textFormat ||
                    FAILED(m_dwrite->CreateTextLayout(text.c_str(), static_cast<UINT32>(text.size()), window.textFormat.get(), unbounded, unbounded, layout.put())) ||
                    FAILED(layout->GetMetrics(&metrics)))
                {
                    return 0.0f;
                }

                return metrics.widthIncludingTrailingWhitespace;
            };

            std::wstring text;
            std::wstring row;
            for (const auto& hint : m_strings.keyHints)
            {
                // A row has no break opportunities, so DirectWrite keeps it whole. A hint wider than a whole row keeps its
                // spaces, so it can still wrap instead of being cut off.
                auto whole = hint;
                std::replace(whole.begin(), whole.end(), L' ', noBreakSpace);
                if (measure(whole) > width)
                {
                    whole = hint;
                }

                if (!row.empty() && measure(row + separator + whole) <= width)
                {
                    row += separator + whole;
                    continue;
                }

                if (!row.empty())
                {
                    text += row + L'\n';
                }

                row = std::move(whole);
            }

            return text + row;
        }

        // On the primary monitor, a switch that the panel or the monitor's caption above it covers moves along its edge to
        // just before or just after them, whichever is nearer and fits: on the monitor, clear of the other switches, and
        // on a side edge between the switch rows of the top and bottom edges. Without room for its issue text, at least
        // the switch itself moves clear; without room for that either, it stays, drawn above the panel so it remains
        // usable. Returns false when a switch or its issue text still overlaps the panel.
        bool Session::MoveSwitchesFromPanel()
        {
            const size_t primary = PrimaryIndex();
            const auto& window = m_windows[primary];
            const RECT& rect = window.details.monitor.rect;
            const float s = window.scale;
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);
            const float gap = panelGap * s;
            const auto panel = PanelRect();
            const D2D1_RECT_F block{ panel.left, panel.top - captionHeight * s, panel.right, panel.bottom };
            const auto edgeOnPrimary = [primary](const OverlayBorder& border) {
                return primary == border.a ? border.edgeOfA : Opposite(border.edgeOfA);
            };

            bool clear = true;
            for (size_t i = 0; i < m_borders.size(); ++i)
            {
                auto& border = m_borders[i];
                if (!HasSwitch(border, primary))
                {
                    continue;
                }

                auto& toggle = border.switches[primary == border.b ? 1 : 0];
                const Edge edge = edgeOnPrimary(border);
                const auto footprint = SwitchFootprint(toggle, edge, s, width);
                if (!Overlaps(footprint, panel) && !Overlaps(toggle, block))
                {
                    continue;
                }

                const bool alongX = IsHorizontal(edge);
                const float low = alongX ? 0.0f : SwitchRow(Edge::Top);
                const float high = alongX ? width : height - SwitchRow(Edge::Bottom);
                const auto shifted = [alongX](const D2D1_RECT_F& r, float shift) {
                    return alongX ? D2D1_RECT_F{ r.left + shift, r.top, r.right + shift, r.bottom } :
                                    D2D1_RECT_F{ r.left, r.top + shift, r.right, r.bottom + shift };
                };

                // `extent` is the switch, with or without its issue text; the other switches are taken the same way.
                const auto fits = [&](const D2D1_RECT_F& extent, bool withIssue) {
                    if ((alongX ? extent.left : extent.top) < low || (alongX ? extent.right : extent.bottom) > high)
                    {
                        return false;
                    }

                    for (size_t j = 0; j < m_borders.size(); ++j)
                    {
                        const auto& other = m_borders[j];
                        if (j == i || !HasSwitch(other, primary))
                        {
                            continue;
                        }

                        const auto& otherToggle = other.switches[primary == other.b ? 1 : 0];
                        if (Overlaps(extent, withIssue ? SwitchFootprint(otherToggle, edgeOnPrimary(other), s, width) : otherToggle))
                        {
                            return false;
                        }
                    }

                    return true;
                };

                // The issue text is kept on the monitor, so near a side it sits differently; it is checked where the
                // switch would land.
                const auto shiftClear = [&](bool withIssue) -> std::optional<float> {
                    const auto& extent = withIssue ? footprint : toggle;
                    const auto landsClear = [&](float shift) {
                        const auto moved = shifted(toggle, shift);
                        const auto landed = withIssue ? SwitchFootprint(moved, edge, s, width) : moved;
                        return fits(landed, withIssue) && !Overlaps(landed, block);
                    };

                    const float before = (alongX ? block.left : block.top) - gap - (alongX ? extent.right : extent.bottom);
                    const float after = (alongX ? block.right : block.bottom) + gap - (alongX ? extent.left : extent.top);
                    const bool beforeFits = landsClear(before);
                    const bool afterFits = landsClear(after);
                    if (beforeFits && (!afterFits || -before <= after))
                    {
                        return before;
                    }

                    return afterFits ? std::optional<float>{ after } : std::nullopt;
                };

                auto shift = shiftClear(true);
                if (!shift && Overlaps(toggle, block))
                {
                    shift = shiftClear(false);
                }

                if (shift)
                {
                    toggle = shifted(toggle, *shift);
                }

                clear = clear && !Overlaps(SwitchFootprint(toggle, edge, s, width), panel);
            }

            return clear;
        }

        // The panel sits on the primary monitor, centered below the monitor's caption, clear of the lines along the edges
        // and of the switches on the top and bottom edges. Switches on the side edges move out of its way instead (see
        // LayOutSwitches). On a small monitor the panel gives up that room, but keeps the caption below the top switches
        // and itself above the bottom ones as long as it can, and the caption on the monitor.
        D2D1_RECT_F Session::PanelRect() const
        {
            const auto& window = m_windows[PrimaryIndex()];
            const RECT& rect = window.details.monitor.rect;
            const float s = window.scale;
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);

            // Room for the lines and their pair numbers, or on the top and bottom edges for a switch and its issue text.
            // Every border of the primary monitor has its switch there (see ToggleMonitor).
            const auto reserve = [&](Edge edge) {
                if (!PrimaryHasBorder(edge))
                {
                    return panelGap * s;
                }

                const float beyondLines = IsHorizontal(edge) ? switchGap + toggleHeight + issueGap + issueHeight : 2.0f * markerLabelSize;
                return markerLengthRatio * (IsHorizontal(edge) ? height : width) + (beyondLines + panelGap) * s;
            };

            const float sideRoom = width - 2.0f * std::max(reserve(Edge::Left), reserve(Edge::Right));
            const float panelWidth = std::min(width, std::max(panelMinWidth * s, std::min(panelMaxWidth * s, sideRoom)));
            // The heights above have room for one row of switches.
            const float moreSwitchRows = m_borders.empty() ? 0.0f : static_cast<float>(PanelSwitchRows(panelWidth, s) - 1) * (buttonHeight + panelSpacing) * s;
            const float panelTall = moreSwitchRows + (m_compactPanel ? compactPanelHeight * s + m_textExtra.keys :
                                                                       panelHeight * s + m_textExtra.instructions + m_textExtra.keys + m_textExtra.legend);
            const float caption = captionHeight * s;
            const float preferredTop = std::max(height * captionPosition, reserve(Edge::Top)) + caption;
            float top = std::min(preferredTop, height - reserve(Edge::Bottom) - panelTall);
            top = std::max(top, SwitchRow(Edge::Top) + caption);
            top = std::max(std::max(0.0f, std::min(caption, height - panelTall)), std::min(top, height - SwitchRow(Edge::Bottom) - panelTall));
            const float left = (width - panelWidth) / 2.0f;
            return { left, top, left + panelWidth, top + panelTall };
        }

        bool Session::PrimaryHasBorder(Edge edge) const
        {
            const size_t index = PrimaryIndex();
            return std::any_of(m_borders.begin(), m_borders.end(), [&](const OverlayBorder& border) {
                return (border.a == index && border.edgeOfA == edge) || (border.b == index && Opposite(border.edgeOfA) == edge);
            });
        }

        // How far from a top or bottom edge of the primary monitor its switches reach, with the lines in front of them.
        // Every border of the primary monitor has its switch there.
        float Session::SwitchRow(Edge edge) const
        {
            if (!PrimaryHasBorder(edge))
            {
                return 0.0f;
            }

            const auto& window = m_windows[PrimaryIndex()];
            const RECT& rect = window.details.monitor.rect;
            return markerLengthRatio * static_cast<float>(rect.bottom - rect.top) + (switchGap + toggleHeight + panelGap) * window.scale;
        }

        std::vector<MarkerRef> Session::Markers(bool enabledOnly) const
        {
            std::vector<MarkerRef> markers;
            for (size_t border = 0; border < m_borders.size(); ++border)
            {
                if (enabledOnly && !m_borders[border].enabled)
                {
                    continue;
                }

                for (size_t pair = 0; pair < 2; ++pair)
                {
                    markers.push_back({ border, pair, false });
                    markers.push_back({ border, pair, true });
                }
            }

            return markers;
        }

        std::optional<MarkerRef> Session::HitTestMarker(size_t index, POINT client) const
        {
            // Lines under the panel are hidden, so they cannot be grabbed there; switches lie above the lines.
            if (index == PrimaryIndex() && Contains(PanelRect(), client))
            {
                return std::nullopt;
            }

            for (const auto& border : m_borders)
            {
                if (HasSwitch(border, index) && Contains(SwitchRect(border, index), client))
                {
                    return std::nullopt;
                }
            }

            // Later lines are drawn over earlier ones, so they take the click first.
            const float margin = markerHitMargin * m_windows[index].scale;
            const auto markers = Markers(true);
            for (auto it = markers.rbegin(); it != markers.rend(); ++it)
            {
                if (MonitorOf(*it) == index && Contains(Inflate(MarkerRect(*it, FractionOf(*it)), margin), client))
                {
                    return *it;
                }
            }

            return std::nullopt;
        }

        void Session::OnButtonDown(size_t index, POINT client)
        {
            // Using the mouse ends keyboard focus on a switch or button.
            if (m_focused)
            {
                m_focused.reset();
                InvalidateAll();
            }

            // Switches first, the last drawn first: they are drawn above everything else, in border order.
            for (size_t i = m_borders.size(); i-- > 0;)
            {
                if (HasSwitch(m_borders[i], index) && Contains(SwitchRect(m_borders[i], index), client))
                {
                    ToggleBorder(i);
                    return;
                }
            }

            auto& window = m_windows[index];
            for (const auto& [rect, action] : window.buttons)
            {
                if (Contains(rect, client))
                {
                    RunAction(action);
                    return;
                }
            }

            // Lines under the panel are hidden, so clicks on the panel stop there.
            if (index == PrimaryIndex() && Contains(PanelRect(), client))
            {
                return;
            }

            if (const auto marker = HitTestMarker(index, client))
            {
                POINT screen = client;
                ClientToScreen(window.hwnd, &screen);
                const double along = IsHorizontal(EdgeOf(*marker)) ? screen.x : screen.y;
                m_grabOffset = along - PositionOf(*marker);
                m_dragged = marker;
                m_selected = marker;
                StopAnimation();
                if (m_previewing)
                {
                    m_callbacks.preview(std::nullopt);
                }

                SetCapture(window.hwnd);
                InvalidateAll();
            }
        }

        void Session::OnDrag(size_t index, POINT client)
        {
            POINT screen = client;
            ClientToScreen(m_windows[index].hwnd, &screen);
            const double along = IsHorizontal(EdgeOf(*m_dragged)) ? screen.x : screen.y;
            SetMarkerPosition(*m_dragged, along - m_grabOffset);
        }

        void Session::OnKey(WPARAM key, bool repeat)
        {
            const bool shift = (GetKeyState(VK_SHIFT) & 0x8000) != 0;
            switch (key)
            {
            case VK_ESCAPE:
            case VK_RETURN:
                // A key still held from opening the overlay (Enter in Command Palette) must not close it right away.
                if (repeat)
                {
                    return;
                }

                // Enter saves, or closes when there is no border to save, as the panel then offers only Close.
                if (key == VK_ESCAPE || m_borders.empty())
                {
                    Finish(false);
                }
                else
                {
                    Save();
                }
                return;
            case VK_TAB:
                MoveFocus(shift);
                return;
            case VK_SPACE:
                // Holding Space must not flip a switch back and forth.
                if (!repeat)
                {
                    PressFocused();
                }
                return;
            case VK_LEFT:
            case VK_UP:
            case VK_RIGHT:
            case VK_DOWN:
                if (m_selected)
                {
                    const double step = shift ? largeStepPixels : 1.0;
                    SetMarkerPosition(*m_selected, PositionOf(*m_selected) + (key == VK_LEFT || key == VK_UP ? -step : step));
                }
                return;
            }
        }

        // Tab order: the lines, then each border's switch, then the panel's switches and buttons as laid out.
        void Session::MoveFocus(bool backward)
        {
            std::vector<FocusStop> stops;
            for (const auto& marker : Markers(true))
            {
                stops.emplace_back(marker);
            }

            for (size_t i = 0; i < m_borders.size(); ++i)
            {
                stops.emplace_back(SwitchRef{ i });
            }

            if (m_borders.empty())
            {
                stops.emplace_back(Action::Cancel);
            }
            else
            {
                for (const Action action : { Action::TogglePreview, Action::ToggleTrail, Action::ToggleEdgeMap, Action::ToggleStop, Action::Cancel, Action::Reset, Action::Save })
                {
                    stops.emplace_back(action);
                }
            }

            const auto current = m_selected ? std::optional<FocusStop>{ *m_selected } : m_focused;
            const auto found = current ? std::find(stops.begin(), stops.end(), *current) : stops.end();
            size_t next = backward ? stops.size() - 1 : 0;
            if (found != stops.end())
            {
                const size_t position = static_cast<size_t>(found - stops.begin());
                next = backward ? (position + stops.size() - 1) % stops.size() : (position + 1) % stops.size();
            }

            m_selected.reset();
            m_focused.reset();
            if (const auto* marker = std::get_if<MarkerRef>(&stops[next]))
            {
                m_selected = *marker;
            }
            else
            {
                m_focused = stops[next];
            }

            StopAnimation();
            InvalidateAll();
        }

        void Session::PressFocused()
        {
            if (!m_focused)
            {
                return;
            }

            if (const auto* toggle = std::get_if<SwitchRef>(&*m_focused))
            {
                ToggleBorder(toggle->border);
            }
            else if (const auto* action = std::get_if<Action>(&*m_focused))
            {
                RunAction(*action);
            }
        }

        bool Session::IsFocused(const FocusStop& stop) const
        {
            return m_focused && *m_focused == stop;
        }

        void Session::ToggleBorder(size_t borderIndex)
        {
            auto& border = m_borders[borderIndex];
            border.enabled = !border.enabled;
            ClearTrail();
            border.keep = border.keep || border.enabled;
            border.savedAsGlue = border.glue;
            if (m_selected && !m_borders[m_selected->border].enabled)
            {
                m_selected.reset();
            }

            StopAnimation();
            InvalidateBorder(borderIndex);
            UpdatePreview();
        }

        void Session::RunAction(Action action)
        {
            switch (action)
            {
            case Action::Save:
                Save();
                break;
            case Action::Reset:
                Reset();
                break;
            case Action::TogglePreview:
                TogglePreview();
                break;
            case Action::ToggleEdgeMap:
                ToggleEdgeMap();
                break;
            case Action::ToggleStop:
                ToggleStop();
                break;
            case Action::ToggleTrail:
                ToggleTrail();
                break;
            default:
                Finish(false);
                break;
            }
        }

        void Session::StartAnimation()
        {
            m_animating = true;
            m_animationStart = Now();
            SetTimer(m_windows[m_borders.front().b].hwnd, animationTimer, animationFrameMs, nullptr);
        }

        void Session::StopAnimation()
        {
            if (!m_animating)
            {
                return;
            }

            m_animating = false;
            if (const HWND hwnd = m_windows[m_borders.front().b].hwnd)
            {
                KillTimer(hwnd, animationTimer);
            }

            InvalidateBorder(0);
        }

        std::optional<Ghost> Session::CurrentGhost() const
        {
            const ULONGLONG elapsed = Now() - m_animationStart;
            if (!m_animating || elapsed >= animationLoops * animationLoopMs)
            {
                return std::nullopt;
            }

            // Loops alternate: the ghost of pair 1 on monitor B slides back into place, then the ghost of pair 2 on
            // monitor A slides forward into place, so both monitors and both pairs are shown. Each holds briefly.
            const bool secondPair = (elapsed / animationLoopMs) % 2 == 1;
            const MarkerRef marker{ 0, secondPair ? 1u : 0u, !secondPair };
            const double target = FractionOf(marker);
            const double forward = target - animationOffset >= 0.0 ? target - animationOffset : target + animationOffset;
            const double backward = target + animationOffset <= 1.0 ? target + animationOffset : target - animationOffset;
            const double start = secondPair ? forward : backward;
            const double progress = std::min(1.0, static_cast<double>(elapsed % animationLoopMs) / (animationLoopMs * 0.75));
            const double eased = 1.0 - std::pow(1.0 - progress, 3.0);
            return Ghost{ marker, start + (target - start) * eased };
        }

        bool Session::CreateTarget(OverlayWindow& window)
        {
            bool created = false;
            if (window.bitmap)
            {
                const auto properties = D2D1::RenderTargetProperties(D2D1_RENDER_TARGET_TYPE_DEFAULT, D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED), 96.0f, 96.0f);
                created = SUCCEEDED(m_d2d->CreateWicBitmapRenderTarget(window.bitmap.get(), properties, window.target.put()));
            }
            else
            {
                RECT client{};
                GetClientRect(window.hwnd, &client);
                const auto properties = D2D1::RenderTargetProperties(D2D1_RENDER_TARGET_TYPE_DEFAULT, D2D1::PixelFormat(), 96.0f, 96.0f);
                const auto hwndProperties = D2D1::HwndRenderTargetProperties(window.hwnd, D2D1::SizeU(client.right, client.bottom));
                winrt::com_ptr<ID2D1HwndRenderTarget> target;
                created = SUCCEEDED(m_d2d->CreateHwndRenderTarget(properties, hwndProperties, target.put()));
                window.target.copy_from(target.get());
            }

            if (!created || FAILED(window.target->CreateSolidColorBrush(Color(textColor), window.brush.put())))
            {
                window.target = nullptr;
                window.brush = nullptr;
                return false;
            }

            window.target->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
            return true;
        }

        winrt::com_ptr<IDWriteTextFormat> Session::MakeFormat(float size, DWRITE_FONT_WEIGHT weight, DWRITE_TEXT_ALIGNMENT alignment) const
        {
            winrt::com_ptr<IDWriteTextFormat> format;
            if (SUCCEEDED(m_dwrite->CreateTextFormat(L"Segoe UI", nullptr, weight, DWRITE_FONT_STYLE_NORMAL, DWRITE_FONT_STRETCH_NORMAL, size, L"", format.put())))
            {
                format->SetTextAlignment(alignment);
                format->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER);
            }

            return format;
        }

        void Session::Render(size_t index)
        {
            auto& window = m_windows[index];
            if (!window.target && !CreateTarget(window))
            {
                return;
            }

            const auto& rect = window.details.monitor.rect;
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);

            window.target->BeginDraw();
            window.target->Clear(Color(backgroundColor));

            const float captionTop = index == PrimaryIndex() ? PanelRect().top - captionHeight * window.scale : height * captionPosition;
            DrawLabel(window, window.details.name, window.captionFormat.get(), { 0.0f, captionTop, width, captionTop + 44.0f * window.scale }, textColor);
            DrawLabel(window, std::to_wstring(rect.right - rect.left) + L" × " + std::to_wstring(rect.bottom - rect.top), window.centeredFormat.get(), { 0.0f, captionTop + 44.0f * window.scale, width, captionTop + 72.0f * window.scale }, secondaryTextColor);

            // The trail stays in the background: lines, switches and the panel are drawn over it.
            DrawTrail(window);

            // Guides first, so the marker lines of every border are drawn on top of them.
            if (m_showEdgeMap)
            {
                DrawEdgeMap(index);
            }

            for (const auto& border : m_borders)
            {
                if ((border.a == index || border.b == index) && (!border.glue || border.enabled))
                {
                    DrawOtherMonitorEnds(window, border, border.b == index);
                }
            }

            for (size_t i = 0; i < m_borders.size(); ++i)
            {
                if (m_borders[i].a == index || m_borders[i].b == index)
                {
                    DrawBorder(index, i);
                }
            }

            window.buttons.clear();
            if (index == PrimaryIndex())
            {
                DrawPanel(window);
            }

            for (size_t i = 0; i < m_borders.size(); ++i)
            {
                if (HasSwitch(m_borders[i], index))
                {
                    DrawSwitch(index, i);
                }
            }

            if (window.target->EndDraw() == D2DERR_RECREATE_TARGET)
            {
                // The device was lost; paint again with a new target.
                window.target = nullptr;
                window.brush = nullptr;
                if (window.hwnd)
                {
                    InvalidateRect(window.hwnd, nullptr, FALSE);
                }
            }
        }

        void Session::DrawBorder(size_t index, size_t borderIndex)
        {
            auto& window = m_windows[index];
            const auto& border = m_borders[borderIndex];
            const bool onB = border.b == index;

            // An unglued pair shows only its switches; its lines appear once it is glued.
            const bool showMarkers = !border.glue || border.enabled;
            for (size_t pair = 0; showMarkers && pair < 2; ++pair)
            {
                const MarkerRef marker{ borderIndex, pair, onB };
                DrawMarker(window, marker, FractionOf(marker), 1.0f);
            }

            if (borderIndex == 0)
            {
                if (const auto ghost = CurrentGhost(); ghost && ghost->marker.onB == onB)
                {
                    DrawMarker(window, ghost->marker, ghost->fraction, 0.45f);
                }
            }
        }

        // Drawn after the panel: where a small monitor leaves no room to keep them apart, the switch stays usable.
        void Session::DrawSwitch(size_t index, size_t borderIndex)
        {
            auto& window = m_windows[index];
            auto* const target = window.target.get();
            auto* const brush = window.brush.get();
            const auto& border = m_borders[borderIndex];
            const bool onB = border.b == index;
            const Edge edge = onB ? Opposite(border.edgeOfA) : border.edgeOfA;
            const float s = window.scale;

            const auto& otherName = m_windows[onB ? border.a : border.b].details.name;
            const auto toggle = SwitchRect(border, index);
            const float radius = (toggle.bottom - toggle.top) / 2.0f;
            brush->SetColor(Color(border.enabled ? border.color : buttonColor));
            target->FillRoundedRectangle(D2D1::RoundedRect(toggle, radius, radius), brush);
            const UINT32 toggleTextColor = border.enabled ? 0x000000 : textColor;
            if (border.glue)
            {
                const float icon = toggle.bottom - toggle.top;
                DrawGlueTube(window, { toggle.left + icon * 0.7f, (toggle.top + toggle.bottom) / 2.0f }, icon * 0.8f, border.enabled ? 0x000000 : border.color, border.enabled ? border.color : buttonColor);
                DrawLabel(window, WithName(border.enabled ? m_strings.glueOn : m_strings.glueOff, otherName), window.pillFormat.get(), { toggle.left + icon * 1.2f, toggle.top, toggle.right - radius, toggle.bottom }, toggleTextColor);
            }
            else
            {
                DrawLabel(window, border.enabled ? m_strings.crossingOn : m_strings.crossingOff, window.pillFormat.get(), toggle, toggleTextColor);
            }

            if (IsFocused(SwitchRef{ borderIndex }))
            {
                DrawFocus(window, toggle, radius);
            }

            // A glued pair has a switch on both monitors; the issue is told only once.
            const auto issue = Issue(border);
            if (index == ToggleMonitor(border) && border.enabled && issue != CalibrationIssue::None)
            {
                const RECT& rect = window.details.monitor.rect;
                const auto issueRect = IssueRect(toggle, edge, s, static_cast<float>(rect.right - rect.left));
                // Not clipped: a long translation may wrap past the rectangle, away from the switch.
                const auto format = edge == Edge::Bottom ? window.issueAboveFormat.get() : window.issueBelowFormat.get();
                DrawLabel(window, IssueText(issue), format, issueRect, IsUsable(issue) ? warningColor : errorColor, false);
            }
        }

        // Thin dashed lines where the other monitor physically ends, according to the lines as placed now.
        // None, one or two of them fall on this monitor's edge.
        void Session::DrawOtherMonitorEnds(OverlayWindow& window, const OverlayBorder& border, bool onB)
        {
            const auto calibration = ToCalibration(border);
            const RECT& a = Rect(border.a);
            const RECT& b = Rect(border.b);
            if (!m_dashStyle || !IsUsable(Validate(calibration, a, b)))
            {
                return;
            }

            const auto map = MakeBorderMap(calibration, a, b);
            const RECT& rect = window.details.monitor.rect;
            const RECT& other = onB ? a : b;
            const Edge edge = onB ? Opposite(border.edgeOfA) : border.edgeOfA;
            const Edge otherEdge = Opposite(edge);
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);
            const float length = endLineLengthRatio * (IsHorizontal(edge) ? height : width);
            const double otherStart = EdgeStart(other, otherEdge);

            window.brush->SetColor(Color(border.color, 0.45f));
            for (const double end : { otherStart, otherStart + EdgeLength(other, otherEdge) })
            {
                const double along = (onB ? map.ToB(end) : map.ToA(end)) - EdgeStart(rect, edge);
                if (along <= 0.0 || along >= EdgeLength(rect, edge))
                {
                    continue;
                }

                const float u = static_cast<float>(along);
                D2D1_POINT_2F from{};
                D2D1_POINT_2F to{};
                switch (edge)
                {
                case Edge::Top:
                    from = { u, 0.0f };
                    to = { u, length };
                    break;
                case Edge::Bottom:
                    from = { u, height };
                    to = { u, height - length };
                    break;
                case Edge::Left:
                    from = { 0.0f, u };
                    to = { length, u };
                    break;
                default:
                    from = { width, u };
                    to = { width - length, u };
                    break;
                }

                window.target->DrawLine(from, to, window.brush.get(), 1.5f * window.scale, m_dashStyle.get());
            }
        }

        // Colors this monitor's edges with what a move across them does under the lines as placed now: the border's
        // color where the pointer crosses, red where it stops. Elsewhere Windows decides by its own layout and
        // settings, so those parts stay uncolored.
        void Session::DrawEdgeMap(size_t index)
        {
            const auto engine = CurrentEngine();
            auto& window = m_windows[index];
            const RECT& rect = window.details.monitor.rect;
            for (size_t edgeIndex = 0; edgeIndex < edgeCount; ++edgeIndex)
            {
                const Edge edge = static_cast<Edge>(edgeIndex);
                for (const auto& part : engine.EdgeParts(rect, edge))
                {
                    UINT32 color = errorColor;
                    if (!part.wall)
                    {
                        const auto border = std::find_if(m_borders.begin(), m_borders.end(), [&](const OverlayBorder& b) {
                            return (b.a == index && b.b == part.target) || (b.b == index && b.a == part.target);
                        });
                        color = border != m_borders.end() ? border->color : textColor;
                    }

                    const float start = static_cast<float>(part.start - EdgeStart(rect, edge));
                    const float end = static_cast<float>(part.end - EdgeStart(rect, edge));
                    window.brush->SetColor(Color(color));
                    window.target->FillRectangle(EdgeStrip(rect, edge, start, end, edgeMapThickness * window.scale), window.brush.get());
                }
            }
        }

        winrt::com_ptr<ID2D1PathGeometry> Session::Polygon(std::initializer_list<D2D1_POINT_2F> points) const
        {
            winrt::com_ptr<ID2D1PathGeometry> geometry;
            winrt::com_ptr<ID2D1GeometrySink> sink;
            if (points.size() < 3 || FAILED(m_d2d->CreatePathGeometry(geometry.put())) || FAILED(geometry->Open(sink.put())))
            {
                return nullptr;
            }

            auto point = points.begin();
            sink->BeginFigure(*point, D2D1_FIGURE_BEGIN_FILLED);
            for (++point; point != points.end(); ++point)
            {
                sink->AddLine(*point);
            }

            sink->EndFigure(D2D1_FIGURE_END_CLOSED);
            return SUCCEEDED(sink->Close()) ? geometry : nullptr;
        }

        // Each dot shrinks, cools from white to blue and fades out as it ages, inside a soft glow.
        void Session::DrawTrail(OverlayWindow& window)
        {
            const ULONGLONG now = Now();
            const float s = window.scale;
            const auto& rect = window.details.monitor.rect;
            const float width = static_cast<float>(rect.right - rect.left);
            const float height = static_cast<float>(rect.bottom - rect.top);
            for (const auto& line : window.wrapLines)
            {
                const float age = std::min(1.0f, static_cast<float>(now - line.time) / static_cast<float>(wrapLineLifetimeMs));
                const float fade = (1.0f - age) * (1.0f - age);
                const D2D1_POINT_2F from = line.horizontal ? D2D1_POINT_2F{ 0.0f, line.position } : D2D1_POINT_2F{ line.position, 0.0f };
                const D2D1_POINT_2F to = line.horizontal ? D2D1_POINT_2F{ width, line.position } : D2D1_POINT_2F{ line.position, height };
                window.brush->SetColor(Color(trailColor, wrapLineGlowAlpha * fade));
                window.target->DrawLine(from, to, window.brush.get(), wrapLineGlowWidth * s);
                window.brush->SetColor(Blend(textColor, trailColor, age, fade));
                window.target->DrawLine(from, to, window.brush.get(), wrapLineWidth * s);
            }

            for (const auto& dot : window.trail)
            {
                const float age = std::min(1.0f, static_cast<float>(now - dot.time) / static_cast<float>(trailLifetimeMs));
                const float life = 1.0f - age;
                const float fade = life * life;
                const float radius = (trailMinRadius + (trailMaxRadius - trailMinRadius) * life) * s;
                const float glow = radius * trailGlowRatio;
                window.brush->SetColor(Color(trailColor, trailGlowAlpha * fade));
                window.target->FillEllipse(D2D1::Ellipse(dot.point, glow, glow), window.brush.get());
                window.brush->SetColor(Blend(textColor, trailColor, age, fade));
                window.target->FillEllipse(D2D1::Ellipse(dot.point, radius, radius), window.brush.get());
            }
        }

        // A ring around the switch or button that has the keyboard focus.
        void Session::DrawFocus(OverlayWindow& window, const D2D1_RECT_F& rect, float radius)
        {
            const float gap = 3.0f * window.scale;
            window.brush->SetColor(Color(textColor));
            window.target->DrawRoundedRectangle(D2D1::RoundedRect(Inflate(rect, gap), radius + gap, radius + gap), window.brush.get(), 2.0f * window.scale);
        }

        // A glue tube pointing right, drawn in a 32 x 32 box centered on `center`: crimped end, tapered body with a
        // label, cap, nozzle and a drop.
        void Session::DrawGlueTube(OverlayWindow& window, D2D1_POINT_2F center, float size, UINT32 bodyColor, UINT32 detailColor)
        {
            auto* const target = window.target.get();
            auto* const brush = window.brush.get();
            const float k = size / 32.0f;
            target->SetTransform(D2D1::Matrix3x2F::Translation(-16.0f, -16.0f) * D2D1::Matrix3x2F::Scale(k, k) * D2D1::Matrix3x2F::Translation(center.x, center.y));

            brush->SetColor(Color(bodyColor));
            target->FillRectangle(D2D1::RectF(2.0f, 10.0f, 5.0f, 22.0f), brush);
            if (const auto body = Polygon({ { 5.0f, 8.5f }, { 21.0f, 7.0f }, { 21.0f, 25.0f }, { 5.0f, 23.5f } }))
            {
                target->FillGeometry(body.get(), brush);
            }

            target->FillRectangle(D2D1::RectF(21.0f, 10.5f, 24.5f, 21.5f), brush);
            if (const auto nozzle = Polygon({ { 24.5f, 13.5f }, { 29.5f, 15.0f }, { 29.5f, 17.0f }, { 24.5f, 18.5f } }))
            {
                target->FillGeometry(nozzle.get(), brush);
            }

            target->FillEllipse(D2D1::Ellipse({ 29.5f, 22.5f }, 1.6f, 2.2f), brush);

            brush->SetColor(Color(detailColor));
            target->FillRectangle(D2D1::RectF(9.0f, 12.0f, 17.0f, 20.0f), brush);
            target->SetTransform(D2D1::Matrix3x2F::Identity());
        }

        void Session::DrawMarker(OverlayWindow& window, const MarkerRef& marker, double fraction, float alpha)
        {
            auto* const target = window.target.get();
            auto* const brush = window.brush.get();
            const auto& border = m_borders[marker.border];
            const auto rect = MarkerRect(marker, fraction);
            const float s = window.scale;
            const bool usable = IsUsable(Issue(border));

            brush->SetColor(Color(border.enabled ? (usable ? border.color : errorColor) : disabledColor, (border.enabled ? 0.95f : 0.4f) * alpha));
            target->FillRectangle(rect, brush);

            // The center line is what the user aligns: white with a dark core, visible on any marker color.
            const bool horizontal = IsHorizontal(EdgeOf(marker));
            const float middle = horizontal ? (rect.left + rect.right) / 2.0f : (rect.top + rect.bottom) / 2.0f;
            const auto from = horizontal ? D2D1::Point2F(middle, rect.top) : D2D1::Point2F(rect.left, middle);
            const auto to = horizontal ? D2D1::Point2F(middle, rect.bottom) : D2D1::Point2F(rect.right, middle);
            brush->SetColor(Color(0xFFFFFF, alpha));
            target->DrawLine(from, to, brush, 3.0f * s);
            brush->SetColor(Color(0x000000, alpha));
            target->DrawLine(from, to, brush, 1.0f);

            if (alpha < 1.0f)
            {
                return;
            }

            if (m_selected == marker)
            {
                brush->SetColor(Color(textColor));
                target->DrawRectangle(Inflate(rect, 3.0f * s), brush, 2.0f * s);
            }

            // Pair number just inside the marker's inner end.
            const float labelSize = markerLabelSize * s;
            const Edge edge = EdgeOf(marker);
            D2D1_RECT_F label{};
            switch (edge)
            {
            case Edge::Top:
                label = { middle - labelSize, rect.bottom, middle + labelSize, rect.bottom + labelSize };
                break;
            case Edge::Bottom:
                label = { middle - labelSize, rect.top - labelSize, middle + labelSize, rect.top };
                break;
            case Edge::Left:
                label = { rect.right, middle - labelSize / 2.0f, rect.right + 2.0f * labelSize, middle + labelSize / 2.0f };
                break;
            default:
                label = { rect.left - 2.0f * labelSize, middle - labelSize / 2.0f, rect.left, middle + labelSize / 2.0f };
                break;
            }

            DrawLabel(window, std::to_wstring(marker.pair + 1), window.centeredFormat.get(), label, border.enabled ? border.color : disabledColor);
        }

        void Session::DrawPanel(OverlayWindow& window)
        {
            auto* const target = window.target.get();
            auto* const brush = window.brush.get();
            const float s = window.scale;
            const float padding = panelPadding * s;
            const D2D1_RECT_F panel = PanelRect();
            const float left = panel.left;
            const float top = panel.top;

            brush->SetColor(Color(panelColor));
            target->FillRoundedRectangle(D2D1::RoundedRect(panel, 8.0f * s, 8.0f * s), brush);

            const float right = panel.right - padding;
            DrawLabel(window, m_strings.title, window.titleFormat.get(), { left + padding, top + padding, right, top + padding + 32.0f * s }, textColor);
            const bool hasBorders = !m_borders.empty();

            // The compact panel, for a monitor too crowded for the full one, leaves out the instructions and the legend.
            if (!m_compactPanel)
            {
                const float instructionsTop = top + padding + 40.0f * s;
                DrawLabel(window, PanelMessage(), window.textFormat.get(), { left + padding, instructionsTop, right, instructionsTop + instructionsHeight * s + m_textExtra.instructions }, textColor);
            }

            if (hasBorders)
            {
                const float keysTop = top + padding + (m_compactPanel ? 40.0f * s : (44.0f + instructionsHeight) * s + m_textExtra.instructions);
                const float keysBottom = keysTop + keysHeight * s + m_textExtra.keys;
                DrawLabel(window, KeysText(), window.textFormat.get(), { left + padding, keysTop, right, keysBottom }, m_storedUnreadable || m_saveFailed ? errorColor : secondaryTextColor);
                if (m_showEdgeMap && !m_compactPanel)
                {
                    const float legendTop = keysBottom + 4.0f * s;
                    DrawLabel(window, m_strings.mapLegend, window.textFormat.get(), { left + padding, legendTop, right, legendTop + legendHeight * s + m_textExtra.legend }, secondaryTextColor);
                }
            }

            std::vector<std::tuple<Action, const std::wstring*, bool>> buttons;
            if (hasBorders)
            {
                buttons = { { Action::Cancel, &m_strings.cancel, false }, { Action::Reset, &m_strings.reset, false }, { Action::Save, &m_strings.save, true } };
            }
            else
            {
                buttons = { { Action::Cancel, &m_strings.close, true } };
            }

            // Laid out right to left, so the primary action ends up rightmost.
            float buttonRight = right;
            const float buttonBottom = panel.bottom - padding;
            if (hasBorders)
            {
                // The switches sit on their own rows above the buttons, in related pairs (see PanelSwitchesPerRow).
                const float radius = buttonHeight * s / 2.0f;
                const std::array<std::tuple<Action, bool, const std::wstring*, const std::wstring*>, panelSwitchCount> switches{ {
                    { Action::TogglePreview, m_previewing, &m_strings.previewOn, &m_strings.previewOff },
                    { Action::ToggleTrail, m_showTrail, &m_strings.trailOn, &m_strings.trailOff },
                    { Action::ToggleEdgeMap, m_showEdgeMap, &m_strings.mapOn, &m_strings.mapOff },
                    { Action::ToggleStop, m_engineOptions.stopWhereNothingIsAcross, &m_strings.stopOn, &m_strings.stopOff },
                } };
                const size_t perRow = PanelSwitchesPerRow(panel.right - panel.left, s);
                const size_t rows = PanelSwitchRows(panel.right - panel.left, s);
                for (size_t i = 0; i < switches.size(); ++i)
                {
                    const auto& [action, on, onText, offText] = switches[i];
                    const float switchBottom = buttonBottom - static_cast<float>(rows - i / perRow) * (buttonHeight + panelSpacing) * s;
                    const float switchLeft = left + padding + static_cast<float>(i % perRow) * (previewToggleWidth + panelSpacing) * s;
                    const D2D1_RECT_F pill{ switchLeft, switchBottom - buttonHeight * s, switchLeft + previewToggleWidth * s, switchBottom };
                    brush->SetColor(Color(on ? accentColor : buttonColor));
                    target->FillRoundedRectangle(D2D1::RoundedRect(pill, radius, radius), brush);
                    DrawLabel(window, on ? *onText : *offText, window.pillFormat.get(), pill, textColor);
                    if (IsFocused(action))
                    {
                        DrawFocus(window, pill, radius);
                    }

                    window.buttons.push_back({ pill, action });
                }
            }

            for (auto it = buttons.rbegin(); it != buttons.rend(); ++it)
            {
                const auto& [action, text, accent] = *it;
                const bool dimmed = action == Action::Save && m_storedUnreadable; // Save does nothing then.
                const D2D1_RECT_F button{ buttonRight - buttonWidth * s, buttonBottom - buttonHeight * s, buttonRight, buttonBottom };
                brush->SetColor(Color(accent && !dimmed ? accentColor : buttonColor));
                target->FillRoundedRectangle(D2D1::RoundedRect(button, 4.0f * s, 4.0f * s), brush);
                DrawLabel(window, *text, window.centeredFormat.get(), button, dimmed ? secondaryTextColor : textColor);
                if (IsFocused(action))
                {
                    DrawFocus(window, button, 4.0f * s);
                }

                window.buttons.push_back({ button, action });
                buttonRight -= (buttonWidth + 12.0f) * s;
            }
        }

        // Clipped by default, so text that does not fit never runs over its neighbors.
        void Session::DrawLabel(OverlayWindow& window, const std::wstring& text, IDWriteTextFormat* format, const D2D1_RECT_F& rect, UINT32 color, bool clip)
        {
            if (!format)
            {
                return;
            }

            window.brush->SetColor(Color(color));
            window.target->DrawTextW(text.c_str(), static_cast<UINT32>(text.size()), format, rect, window.brush.get(), clip ? D2D1_DRAW_TEXT_OPTIONS_CLIP : D2D1_DRAW_TEXT_OPTIONS_NONE);
        }

        void Session::InvalidateBorder(size_t borderIndex) const
        {
            if (borderIndex >= m_borders.size())
            {
                return;
            }

            for (const size_t monitor : { m_borders[borderIndex].a, m_borders[borderIndex].b })
            {
                if (m_windows[monitor].hwnd)
                {
                    InvalidateRect(m_windows[monitor].hwnd, nullptr, FALSE);
                }
            }
        }

        void Session::InvalidateAll() const
        {
            for (const auto& window : m_windows)
            {
                if (window.hwnd)
                {
                    InvalidateRect(window.hwnd, nullptr, FALSE);
                }
            }
        }

        const std::wstring& Session::IssueText(CalibrationIssue issue) const
        {
            switch (issue)
            {
            case CalibrationIssue::MarkersTooClose:
                return m_strings.tooClose;
            case CalibrationIssue::OrderReversed:
                return m_strings.reversed;
            case CalibrationIssue::ScaleOutOfRange:
                return m_strings.scale;
            default:
                return m_strings.invalid;
            }
        }

        LRESULT CALLBACK Session::WindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
        {
            if (message == WM_NCCREATE)
            {
                const auto* const create = reinterpret_cast<const CREATESTRUCTW*>(lParam);
                SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(create->lpCreateParams));
            }

            auto* const self = reinterpret_cast<Session*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
            if (self)
            {
                if (const auto index = self->IndexOf(hwnd))
                {
                    return self->HandleMessage(*index, message, wParam, lParam);
                }
            }

            return DefWindowProcW(hwnd, message, wParam, lParam);
        }

        LRESULT Session::HandleMessage(size_t index, UINT message, WPARAM wParam, LPARAM lParam)
        {
            const HWND hwnd = m_windows[index].hwnd;
            switch (message)
            {
            case WM_PAINT:
            {
                PAINTSTRUCT paint;
                BeginPaint(hwnd, &paint);
                Render(index);
                EndPaint(hwnd, &paint);
                return 0;
            }
            case WM_ERASEBKGND:
                return 1;
            case WM_SETCURSOR:
                if (LOWORD(lParam) == HTCLIENT)
                {
                    POINT cursor{};
                    GetCursorPos(&cursor);
                    ScreenToClient(hwnd, &cursor);
                    const auto marker = m_dragged ? m_dragged : HitTestMarker(index, cursor);
                    SetCursor(LoadCursorW(nullptr, !marker ? IDC_ARROW : (IsHorizontal(EdgeOf(*marker)) ? IDC_SIZEWE : IDC_SIZENS)));
                    return TRUE;
                }
                break;
            case WM_LBUTTONDOWN:
                OnButtonDown(index, { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) });
                return 0;
            case WM_MOUSEMOVE:
                if (m_dragged && GetCapture() == hwnd)
                {
                    OnDrag(index, { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) });
                }
                else
                {
                    AddTrailDots(index, { GET_X_LPARAM(lParam), GET_Y_LPARAM(lParam) });
                }
                return 0;
            case WM_LBUTTONUP:
                if (m_dragged)
                {
                    ReleaseCapture();
                }
                return 0;
            case WM_CAPTURECHANGED:
                m_dragged.reset();
                UpdatePreview();
                return 0;
            case WM_KEYDOWN:
                OnKey(wParam, (HIWORD(lParam) & KF_REPEAT) != 0);
                return 0;
            case WM_TIMER:
                if (wParam == animationTimer)
                {
                    if (!CurrentGhost())
                    {
                        StopAnimation();
                    }

                    InvalidateBorder(0);
                }
                else if (wParam == trailTimer)
                {
                    UpdateTrail(index);
                }
                return 0;
            case WM_DISPLAYCHANGE:
            case WM_CLOSE:
                // A display change makes the whole layout stale.
                Finish(false);
                return 0;
            }

            return DefWindowProcW(hwnd, message, wParam, lParam);
        }
    }

    std::vector<winrt::com_ptr<IWICBitmap>> RenderSnapshot(IWICImagingFactory* wic, const SnapshotRequest& request)
    {
        const CalibrationOverlayCallbacks callbacks{ [] {}, [](std::optional<std::vector<BorderCalibration>>) {}, [](bool) {}, [](bool) {} };
        Session session{ callbacks, request.engineOptions };
        return session.Snapshot(wic, request);
    }

    CalibrationOverlay::CalibrationOverlay(CalibrationOverlayCallbacks callbacks) :
        m_callbacks(std::move(callbacks))
    {
    }

    CalibrationOverlay::~CalibrationOverlay()
    {
        Close();
    }

    void CalibrationOverlay::Show()
    {
        std::scoped_lock lifecycle{ m_lifecycleMutex };
        {
            // While the thread id is set, the thread is guaranteed to read this message (see Run).
            std::scoped_lock lock{ m_threadMutex };
            if (m_threadId)
            {
                PostThreadMessageW(m_threadId, bringToFrontMessage, 0, 0);
                return;
            }
        }

        if (m_thread.joinable())
        {
            m_thread.join();
        }

        const HANDLE started = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        if (!started)
        {
            Logger::error(L"Failed to create the calibration overlay startup event. Error: {}", GetLastError());
            return;
        }

        try
        {
            m_thread = std::thread([this, started]() { Run(started); });
            WaitForSingleObject(started, INFINITE);
        }
        catch (const std::system_error& error)
        {
            Logger::error("Failed to start the calibration overlay thread: {}", error.what());
        }

        CloseHandle(started);
    }

    void CalibrationOverlay::SetEngineOptions(EngineOptions options) noexcept
    {
        m_stopWhereNothingIsAcross = options.stopWhereNothingIsAcross;
    }

    void CalibrationOverlay::Close()
    {
        std::scoped_lock lifecycle{ m_lifecycleMutex };
        {
            std::scoped_lock lock{ m_threadMutex };
            if (m_threadId)
            {
                PostThreadMessageW(m_threadId, closeMessage, 0, 0);
            }
        }

        if (m_thread.joinable())
        {
            m_thread.join();
        }
    }

    void CalibrationOverlay::Run(HANDLE started)
    {
        // Overlay windows must cover each monitor exactly, in physical pixels.
        SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        // Create the message queue before publishing the thread id, so PostThreadMessage cannot fail.
        MSG message;
        PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);
        m_threadId = GetCurrentThreadId();
        SetEvent(started);

        bool closeRequested = false;
        for (;;)
        {
            bool reopenRequested = false;
            {
                Session session{ m_callbacks, EngineOptions{ m_stopWhereNothingIsAcross } };
                if (session.Open())
                {
                    while (GetMessageW(&message, nullptr, 0, 0) > 0)
                    {
                        if (!message.hwnd && message.message == closeMessage)
                        {
                            closeRequested = true;
                            session.Finish(false);
                            continue;
                        }

                        if (!message.hwnd && message.message == bringToFrontMessage)
                        {
                            // A Calibrate click that arrives while this session closes opens the next one.
                            if (session.Finished())
                            {
                                reopenRequested = true;
                            }
                            else
                            {
                                session.BringToFront();
                            }

                            continue;
                        }

                        TranslateMessage(&message);
                        DispatchMessageW(&message);
                    }

                    m_callbacks.closed(session.Saved());
                }
            }

            // Show and Close post only while holding the lock and seeing the thread id, so after this check no
            // request can be lost: it is either in the queue now, or it finds the id cleared and starts a new thread.
            std::scoped_lock lock{ m_threadMutex };
            const bool closePending = PeekMessageW(&message, nullptr, closeMessage, closeMessage, PM_REMOVE) != FALSE;
            const bool reopenPending = PeekMessageW(&message, nullptr, bringToFrontMessage, bringToFrontMessage, PM_REMOVE) != FALSE;
            if (closeRequested || closePending || !(reopenRequested || reopenPending))
            {
                m_threadId = 0;
                return;
            }
        }
    }
}
