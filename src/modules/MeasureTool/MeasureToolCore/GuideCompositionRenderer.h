#pragma once

#include "DxgiAPI.h"
#include "GuideModel.h"
#include "GuideVisualMetrics.h"

#include <winrt/Windows.System.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>

#include <chrono>
#include <optional>
#include <unordered_map>

class GuideCompositionRenderer final
{
public:
    struct InteractionVisualState
    {
        GuideModel::Interaction interaction;
        POINT systemPointer{};
        bool snapped = false;
        GuideModel::DismissalEdge dismissalEdge = GuideModel::DismissalEdge::None;
    };

    GuideCompositionRenderer(
        DxgiAPI* dxgiAPI,
        const winrt::Windows::UI::Composition::Compositor& compositor,
        const winrt::Windows::System::DispatcherQueue& dispatcherQueue,
        D2D1::ColorF lineColor);
    ~GuideCompositionRenderer();

    void AddMonitor(const GuideModel::Monitor& monitor, HWND renderWindow, HWND labelWindow);
    void RemoveAllMonitors();
    void SyncGuides(const std::vector<GuideModel::Guide>& guides, bool animateNewGuides);
    void SetHoveredGuide(std::optional<GuideModel::GuideId> guideId);
    void SetInteraction(std::optional<InteractionVisualState> state);
    void SetEditMode(bool enabled);
    void SetToolbarBoundingBox(RECT bounds);
    void ClearGuides(bool animate);
    void SetLineColor(D2D1::ColorF lineColor);

private:
    struct MonitorVisuals
    {
        GuideModel::Monitor monitor;
        RECT workArea{};
        HWND renderWindow = nullptr;
        HWND labelWindow = nullptr;
        winrt::Windows::UI::Composition::CompositionTarget target{ nullptr };
        winrt::Windows::UI::Composition::CompositionTarget labelTarget{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual root{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual labelRoot{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual guideLayer{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual distanceLabelLayer{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual interactionLayer{ nullptr };
        uint32_t dpi = GuideVisualMetrics::DefaultDpi;
        SIZE labelSize{};
    };

    struct GuideVisuals
    {
        GuideModel::Guide guide;
        winrt::Windows::UI::Composition::SpriteVisual rail{ nullptr };
        winrt::Windows::UI::Composition::SpriteVisual line{ nullptr };
        bool railVisible = false;
    };

    struct PreviewVisuals
    {
        GuideModel::MonitorId monitorId = 0;
        GuideModel::Orientation orientation = GuideModel::Orientation::Horizontal;
        winrt::Windows::UI::Composition::SpriteVisual rail{ nullptr };
        winrt::Windows::UI::Composition::SpriteVisual line{ nullptr };
    };

    struct LabelVisuals
    {
        GuideModel::MonitorId monitorId = 0;
        uint32_t dpi = GuideVisualMetrics::DefaultDpi;
        GuideVisualMetrics::LabelMetrics metrics;
        winrt::Windows::UI::Composition::CompositionDrawingSurface surface{ nullptr };
        winrt::Windows::UI::Composition::CompositionSurfaceBrush brush{ nullptr };
        winrt::Windows::UI::Composition::CompositionDrawingSurface maskSurface{ nullptr };
        winrt::Windows::UI::Composition::CompositionSurfaceBrush maskBrush{ nullptr };
        winrt::Windows::UI::Composition::CompositionMaskBrush backgroundBrush{ nullptr };
        winrt::Windows::UI::Composition::SpriteVisual backgroundVisual{ nullptr };
        winrt::Windows::UI::Composition::SpriteVisual contentVisual{ nullptr };
        winrt::Windows::UI::Composition::ContainerVisual visual{ nullptr };
        winrt::com_ptr<IDWriteTextFormat> textFormat;
        winrt::com_ptr<IDWriteTextLayout> textLayout;
        std::wstring text;
        std::wstring drawnText;
        bool removal = false;
        bool drawnRemoval = false;
        bool darkMode = false;
        bool highContrast = false;
        bool shown = false;
        bool axisPrefix = false;
        bool drawnAxisPrefix = false;
    };

    struct DistanceLabelVisuals
    {
        GuideModel::MonitorId monitorId = 0;
        GuideModel::Orientation orientation = GuideModel::Orientation::Horizontal;
        size_t ordinal = 0;
        GuideModel::DistanceSegment segment;
        LabelVisuals label;
    };

    struct RemovalVisuals
    {
        GuideModel::MonitorId monitorId = 0;
        GuideModel::DismissalEdge edge = GuideModel::DismissalEdge::None;
        winrt::Windows::UI::Composition::SpriteVisual visual{ nullptr };
    };

    MonitorVisuals* FindMonitor(GuideModel::MonitorId id);
    const MonitorVisuals* FindMonitor(GuideModel::MonitorId id) const;
    void CreateGuideVisual(const GuideModel::Guide& guide, bool animate);
    void RemoveGuideVisual(GuideModel::GuideId id, bool animate);
    void PositionGuideVisual(GuideVisuals& visuals, int coordinate);
    void SetRailVisible(GuideVisuals& visuals, bool visible);
    void CreateOrMovePreview(const InteractionVisualState& state);
    void RemovePreview();
    LabelVisuals CreateLabelVisual(
        const MonitorVisuals& monitor,
        bool removal,
        std::wstring_view text,
        bool axisPrefix);
    void UpdateLabelText(LabelVisuals& label, std::wstring_view text);
    void ResizeLabelVisual(LabelVisuals& label, const GuideVisualMetrics::LabelMetrics& metrics);
    void UpdateDistanceLabels();
    void ClearDistanceLabels();
    void PositionDistanceLabel(
        DistanceLabelVisuals& visuals,
        const GuideModel::DistanceSegment& segment);
    void ShowLabel(const InteractionVisualState& state);
    void HideLabel();
    void ScheduleLabelRedraw();
    void DrawPendingLabels();
    bool DrawLabelMask(LabelVisuals& label);
    bool DrawLabel(LabelVisuals& label);
    winrt::Windows::UI::Composition::CompositionBrush CreateLabelBackgroundBrush(bool removal);
    void PositionLabelWindow(MonitorVisuals& monitor, float x, float y, const GuideVisualMetrics::LabelMetrics& metrics);
    void HideLabelWindow(MonitorVisuals& monitor);
    void ShowSnapPulse(const InteractionVisualState& state);
    void UpdateRemovalAffordance(const InteractionVisualState& state);
    void HideRemovalAffordance();
    void AnimateOpacity(
        const winrt::Windows::UI::Composition::Visual& visual,
        float from,
        float to,
        std::chrono::milliseconds duration);
    void AnimateRail(
        const winrt::Windows::UI::Composition::SpriteVisual& rail,
        GuideModel::Orientation orientation,
        bool visible);
    winrt::Windows::UI::Color ToColor(D2D1::ColorF color, uint8_t alpha) const;
    bool AnimationsEnabled() const;

    static constexpr float RailThickness = 7.0f;
    static constexpr float RemovalThickness = 16.0f;
    static constexpr auto LabelRedrawInterval = std::chrono::milliseconds{ 16 };

    DxgiAPI* _dxgiAPI = nullptr;
    winrt::Windows::UI::Composition::Compositor _compositor{ nullptr };
    winrt::Windows::System::DispatcherQueueTimer _labelRedrawTimer{ nullptr };
    winrt::Windows::UI::Composition::CompositionGraphicsDevice _graphicsDevice{ nullptr };
    winrt::Windows::UI::Composition::CompositionColorBrush _lineBrush{ nullptr };
    winrt::Windows::UI::Composition::CompositionColorBrush _railBrush{ nullptr };
    winrt::Windows::UI::Composition::CompositionColorBrush _removalBrush{ nullptr };
    D2D1::ColorF _lineColor;
    winrt::event_token _deviceReplacedToken{};
    winrt::event_token _labelRedrawTimerToken{};
    bool _labelRedrawScheduled = false;

    std::unordered_map<GuideModel::MonitorId, MonitorVisuals> _monitors;
    std::unordered_map<GuideModel::GuideId, GuideVisuals> _guideVisuals;
    std::vector<GuideModel::Guide> _syncedGuides;
    std::vector<DistanceLabelVisuals> _distanceLabels;
    std::optional<GuideModel::GuideId> _hoveredGuide;
    std::optional<InteractionVisualState> _interaction;
    std::optional<PreviewVisuals> _preview;
    std::optional<LabelVisuals> _label;
    std::optional<RemovalVisuals> _removal;
    RECT _toolbarBounds{};
    bool _editMode = true;
};
