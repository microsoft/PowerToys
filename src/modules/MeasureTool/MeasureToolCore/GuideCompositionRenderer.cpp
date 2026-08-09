#include "pch.h"

#include "GuideCompositionRenderer.h"
#include "Measurement.h"
#include "MeasurementTooltipStyle.h"

#include <common/Display/dpi_aware.h>
#include <common/Themes/windows_colors.h>
#include <common/utils/MsWindowsSettings.h>

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <limits>

#undef GetNextSibling

namespace muxc = winrt::Windows::UI::Composition;

namespace
{
    constexpr auto FastDuration = std::chrono::milliseconds{ 90 };
    constexpr auto NormalDuration = std::chrono::milliseconds{ 140 };

    bool IsHighContrast()
    {
        HIGHCONTRASTW highContrast{ .cbSize = sizeof(HIGHCONTRASTW) };
        return SystemParametersInfoW(SPI_GETHIGHCONTRAST, sizeof(highContrast), &highContrast, 0) &&
               (highContrast.dwFlags & HCF_HIGHCONTRASTON);
    }

    winrt::Windows::UI::Color SystemColor(int index)
    {
        const COLORREF color = GetSysColor(index);
        return winrt::Windows::UI::ColorHelper::FromArgb(
            255,
            GetRValue(color),
            GetGValue(color),
            GetBValue(color));
    }

    winrt::Windows::UI::Color Color(uint8_t alpha, uint8_t red, uint8_t green, uint8_t blue)
    {
        return winrt::Windows::UI::ColorHelper::FromArgb(alpha, red, green, blue);
    }

    D2D1::ColorF D2DColor(const MeasurementTooltipStyle::Color& color)
    {
        return D2D1::ColorF(color.red, color.green, color.blue, color.alpha);
    }

    winrt::Windows::UI::Color CompositionColor(const MeasurementTooltipStyle::Color& color)
    {
        const auto toByte = [](float channel) {
            return static_cast<uint8_t>(std::round(std::clamp(channel, 0.0f, 1.0f) * 255.0f));
        };
        return Color(toByte(color.alpha), toByte(color.red), toByte(color.green), toByte(color.blue));
    }
}

GuideCompositionRenderer::GuideCompositionRenderer(
    DxgiAPI* dxgiAPI,
    const muxc::Compositor& compositor,
    const winrt::Windows::System::DispatcherQueue& dispatcherQueue,
    D2D1::ColorF lineColor) :
    _dxgiAPI{ dxgiAPI },
    _compositor{ compositor },
    _lineColor{ lineColor }
{
    winrt::com_ptr<ABI::Windows::UI::Composition::ICompositionGraphicsDevice> graphicsDevice;
    winrt::check_hresult(
        _compositor.as<ABI::Windows::UI::Composition::ICompositorInterop>()->CreateGraphicsDevice(
            _dxgiAPI->d2dDevice1.get(),
            graphicsDevice.put()));
    _graphicsDevice = graphicsDevice.as<muxc::CompositionGraphicsDevice>();

    _lineBrush = _compositor.CreateColorBrush(ToColor(_lineColor, 255));
    _railBrush = _compositor.CreateColorBrush(ToColor(_lineColor, 96));
    _removalBrush = _compositor.CreateColorBrush(ToColor(_lineColor, 140));

    _labelRedrawTimer = dispatcherQueue.CreateTimer();
    _labelRedrawTimer.Interval(LabelRedrawInterval);
    _labelRedrawTimer.IsRepeating(false);
    _labelRedrawTimerToken = _labelRedrawTimer.Tick([this](auto&&, auto&&) {
        _labelRedrawScheduled = false;
        DrawPendingLabels();
    });

    _deviceReplacedToken = _graphicsDevice.RenderingDeviceReplaced([this](auto&&, auto&&) {
        if (_label)
        {
            DrawLabelMask(*_label);
            _label->drawnText.clear();
        }
        for (auto& distanceLabel : _distanceLabels)
        {
            DrawLabelMask(distanceLabel.label);
            distanceLabel.label.drawnText.clear();
        }
        DrawPendingLabels();
    });
}

GuideCompositionRenderer::~GuideCompositionRenderer()
{
    if (_labelRedrawTimer)
    {
        _labelRedrawTimer.Stop();
        _labelRedrawTimer.Tick(_labelRedrawTimerToken);
        _labelRedrawTimer = nullptr;
    }
    if (_graphicsDevice)
    {
        _graphicsDevice.RenderingDeviceReplaced(_deviceReplacedToken);
    }
    RemoveAllMonitors();
}

void GuideCompositionRenderer::AddMonitor(const GuideModel::Monitor& monitor, HWND renderWindow, HWND labelWindow)
{
    auto compositorDesktopInterop =
        _compositor.as<ABI::Windows::UI::Composition::Desktop::ICompositorDesktopInterop>();
    const auto createTarget = [&](HWND window) {
        winrt::com_ptr<ABI::Windows::UI::Composition::Desktop::IDesktopWindowTarget> desktopTarget;
        winrt::check_hresult(
            compositorDesktopInterop->CreateDesktopWindowTarget(window, false, desktopTarget.put()));
        return desktopTarget.as<muxc::CompositionTarget>();
    };

    MonitorVisuals visuals;
    visuals.monitor = monitor;
    MONITORINFO monitorInfo{ .cbSize = sizeof(MONITORINFO) };
    visuals.workArea =
        GetMonitorInfoW(reinterpret_cast<HMONITOR>(monitor.id), &monitorInfo) ?
            monitorInfo.rcWork :
            monitor.bounds;
    visuals.renderWindow = renderWindow;
    visuals.labelWindow = labelWindow;
    visuals.target = createTarget(renderWindow);
    visuals.labelTarget = createTarget(labelWindow);
    visuals.root = _compositor.CreateContainerVisual();
    visuals.root.RelativeSizeAdjustment({ 1.0f, 1.0f });
    visuals.labelRoot = _compositor.CreateContainerVisual();
    visuals.labelRoot.RelativeSizeAdjustment({ 1.0f, 1.0f });
    visuals.guideLayer = _compositor.CreateContainerVisual();
    visuals.guideLayer.RelativeSizeAdjustment({ 1.0f, 1.0f });
    visuals.distanceLabelLayer = _compositor.CreateContainerVisual();
    visuals.distanceLabelLayer.RelativeSizeAdjustment({ 1.0f, 1.0f });
    visuals.interactionLayer = _compositor.CreateContainerVisual();
    visuals.interactionLayer.RelativeSizeAdjustment({ 1.0f, 1.0f });
    DPIAware::GetScreenDPIForWindow(renderWindow, visuals.dpi);

    visuals.root.Children().InsertAtTop(visuals.guideLayer);
    visuals.root.Children().InsertAtTop(visuals.distanceLabelLayer);
    visuals.root.Children().InsertAtTop(visuals.interactionLayer);
    visuals.target.Root(visuals.root);
    visuals.labelTarget.Root(visuals.labelRoot);

    _monitors.insert_or_assign(monitor.id, std::move(visuals));
}

void GuideCompositionRenderer::RemoveAllMonitors()
{
    if (_labelRedrawTimer && _labelRedrawScheduled)
    {
        _labelRedrawTimer.Stop();
    }
    _labelRedrawScheduled = false;
    _interaction.reset();
    HideLabel();
    ClearDistanceLabels();
    _preview.reset();
    _removal.reset();
    _guideVisuals.clear();

    for (auto& [_, monitor] : _monitors)
    {
        if (monitor.labelTarget)
        {
            monitor.labelTarget.Root(nullptr);
        }
        if (monitor.target)
        {
            monitor.target.Root(nullptr);
        }
    }
    _monitors.clear();
}

void GuideCompositionRenderer::SyncGuides(const std::vector<GuideModel::Guide>& guides, bool animateNewGuides)
{
    _syncedGuides = guides;
    std::vector<GuideModel::GuideId> removedIds;
    for (const auto& [id, _] : _guideVisuals)
    {
        const bool stillExists = std::ranges::any_of(guides, [id](const auto& guide) {
            return guide.id == id;
        });
        if (!stillExists)
        {
            removedIds.push_back(id);
        }
    }
    for (const auto id : removedIds)
    {
        RemoveGuideVisual(id, true);
    }

    for (const auto& guide : guides)
    {
        auto iterator = _guideVisuals.find(guide.id);
        if (iterator == _guideVisuals.end())
        {
            CreateGuideVisual(guide, animateNewGuides);
            continue;
        }

        auto& visuals = iterator->second;
        if (visuals.guide.monitorId != guide.monitorId || visuals.guide.orientation != guide.orientation)
        {
            RemoveGuideVisual(guide.id, false);
            CreateGuideVisual(guide, animateNewGuides);
            continue;
        }

        visuals.guide = guide;
        PositionGuideVisual(visuals, guide.coordinate);
    }
    UpdateDistanceLabels();
}

void GuideCompositionRenderer::SetHoveredGuide(std::optional<GuideModel::GuideId> guideId)
{
    if (_hoveredGuide == guideId)
    {
        return;
    }

    if (_hoveredGuide)
    {
        if (auto iterator = _guideVisuals.find(*_hoveredGuide); iterator != _guideVisuals.end())
        {
            SetRailVisible(iterator->second, false);
        }
    }

    _hoveredGuide = guideId;
    if (_hoveredGuide)
    {
        if (auto iterator = _guideVisuals.find(*_hoveredGuide); iterator != _guideVisuals.end())
        {
            SetRailVisible(iterator->second, true);
        }
    }
}

void GuideCompositionRenderer::SetInteraction(std::optional<InteractionVisualState> state)
{
    const bool enteredSnap = state &&
                             state->snapped &&
                             (!_interaction ||
                              !_interaction->snapped ||
                              state->interaction.coordinate != _interaction->interaction.coordinate);
    _interaction = state;

    if (!_interaction)
    {
        RemovePreview();
        HideLabel();
        HideRemovalAffordance();
        for (auto& [_, guide] : _guideVisuals)
        {
            PositionGuideVisual(guide, guide.guide.coordinate);
            SetRailVisible(guide, _hoveredGuide && *_hoveredGuide == guide.guide.id);
        }
        UpdateDistanceLabels();
        return;
    }

    if (_interaction->interaction.kind == GuideModel::InteractionKind::Placement)
    {
        CreateOrMovePreview(*_interaction);
    }
    else if (_interaction->interaction.guideId)
    {
        RemovePreview();
        auto iterator = _guideVisuals.find(*_interaction->interaction.guideId);
        if (iterator != _guideVisuals.end() &&
            iterator->second.guide.monitorId != _interaction->interaction.monitor.id)
        {
            auto transferredGuide = iterator->second.guide;
            transferredGuide.monitorId = _interaction->interaction.monitor.id;
            transferredGuide.coordinate = _interaction->interaction.coordinate;
            RemoveGuideVisual(transferredGuide.id, false);
            CreateGuideVisual(transferredGuide, false);
            iterator = _guideVisuals.find(transferredGuide.id);
        }

        if (iterator != _guideVisuals.end())
        {
            PositionGuideVisual(iterator->second, _interaction->interaction.coordinate);
            SetRailVisible(iterator->second, true);
        }
    }

    UpdateDistanceLabels();
    ShowLabel(*_interaction);
    UpdateRemovalAffordance(*_interaction);
    if (enteredSnap)
    {
        ShowSnapPulse(*_interaction);
    }
}

void GuideCompositionRenderer::SetEditMode(bool enabled)
{
    if (_editMode == enabled)
    {
        return;
    }

    _editMode = enabled;
    UpdateDistanceLabels();
}

void GuideCompositionRenderer::SetToolbarBoundingBox(RECT bounds)
{
    _toolbarBounds = bounds;
    UpdateDistanceLabels();
}

void GuideCompositionRenderer::ClearGuides(bool animate)
{
    _syncedGuides.clear();
    std::vector<GuideModel::GuideId> guideIds;
    guideIds.reserve(_guideVisuals.size());
    for (const auto& [id, _] : _guideVisuals)
    {
        guideIds.push_back(id);
    }
    for (const auto id : guideIds)
    {
        RemoveGuideVisual(id, animate);
    }
    SetInteraction(std::nullopt);
    SetHoveredGuide(std::nullopt);
    ClearDistanceLabels();
}

void GuideCompositionRenderer::SetLineColor(D2D1::ColorF lineColor)
{
    _lineColor = lineColor;
    _lineBrush.Color(ToColor(_lineColor, 255));
    _railBrush.Color(ToColor(_lineColor, 96));
    _removalBrush.Color(ToColor(_lineColor, 140));
    if (_label)
    {
        _label->drawnText.clear();
    }
    for (auto& distanceLabel : _distanceLabels)
    {
        distanceLabel.label.drawnText.clear();
    }
    DrawPendingLabels();
}

GuideCompositionRenderer::MonitorVisuals* GuideCompositionRenderer::FindMonitor(GuideModel::MonitorId id)
{
    const auto iterator = _monitors.find(id);
    return iterator == _monitors.end() ? nullptr : &iterator->second;
}

const GuideCompositionRenderer::MonitorVisuals* GuideCompositionRenderer::FindMonitor(GuideModel::MonitorId id) const
{
    const auto iterator = _monitors.find(id);
    return iterator == _monitors.end() ? nullptr : &iterator->second;
}

void GuideCompositionRenderer::CreateGuideVisual(const GuideModel::Guide& guide, bool animate)
{
    auto* monitor = FindMonitor(guide.monitorId);
    if (!monitor)
    {
        return;
    }

    GuideVisuals visuals;
    visuals.guide = guide;
    visuals.rail = _compositor.CreateSpriteVisual();
    visuals.rail.Brush(_railBrush);
    visuals.rail.Opacity(0.0f);
    visuals.line = _compositor.CreateSpriteVisual();
    visuals.line.Brush(_lineBrush);

    monitor->guideLayer.Children().InsertAtTop(visuals.rail);
    monitor->guideLayer.Children().InsertAtTop(visuals.line);
    PositionGuideVisual(visuals, guide.coordinate);

    if (animate && AnimationsEnabled())
    {
        const auto axisScale = guide.orientation == GuideModel::Orientation::Horizontal ?
                                   winrt::float3{ 0.0f, 1.0f, 1.0f } :
                                   winrt::float3{ 1.0f, 0.0f, 1.0f };
        visuals.line.Scale(axisScale);
        AnimateOpacity(visuals.line, 0.0f, 1.0f, FastDuration);

        auto scale = _compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(1.0f, { 1.0f, 1.0f, 1.0f });
        scale.Duration(NormalDuration);
        visuals.line.StartAnimation(L"Scale", scale);
    }

    _guideVisuals.insert_or_assign(guide.id, std::move(visuals));
}

void GuideCompositionRenderer::RemoveGuideVisual(GuideModel::GuideId id, bool animate)
{
    const auto iterator = _guideVisuals.find(id);
    if (iterator == _guideVisuals.end())
    {
        return;
    }

    auto visuals = std::move(iterator->second);
    _guideVisuals.erase(iterator);
    auto* monitor = FindMonitor(visuals.guide.monitorId);
    if (!monitor)
    {
        return;
    }

    if (!animate || !AnimationsEnabled())
    {
        monitor->guideLayer.Children().Remove(visuals.line);
        monitor->guideLayer.Children().Remove(visuals.rail);
        return;
    }

    auto batch = _compositor.CreateScopedBatch(muxc::CompositionBatchTypes::Animation);
    AnimateOpacity(visuals.line, visuals.line.Opacity(), 0.0f, FastDuration);
    AnimateOpacity(visuals.rail, visuals.rail.Opacity(), 0.0f, FastDuration);

    auto collapse = _compositor.CreateVector3KeyFrameAnimation();
    const auto finalScale = visuals.guide.orientation == GuideModel::Orientation::Horizontal ?
                                winrt::float3{ 1.0f, 0.0f, 1.0f } :
                                winrt::float3{ 0.0f, 1.0f, 1.0f };
    collapse.InsertKeyFrame(1.0f, finalScale);
    collapse.Duration(FastDuration);
    visuals.line.StartAnimation(L"Scale", collapse);

    const auto layer = monitor->guideLayer;
    const auto line = visuals.line;
    const auto rail = visuals.rail;
    batch.Completed([layer, line, rail](auto&&, auto&&) {
        layer.Children().Remove(line);
        layer.Children().Remove(rail);
    });
    batch.End();
}

void GuideCompositionRenderer::PositionGuideVisual(GuideVisuals& visuals, int coordinate)
{
    const auto* monitor = FindMonitor(visuals.guide.monitorId);
    if (!monitor)
    {
        return;
    }

    const float width = static_cast<float>(monitor->monitor.bounds.right - monitor->monitor.bounds.left);
    const float height = static_cast<float>(monitor->monitor.bounds.bottom - monitor->monitor.bounds.top);
    const float axis = static_cast<float>(coordinate);

    winrt::float3 lineOffset;
    winrt::float3 railOffset;
    if (visuals.guide.orientation == GuideModel::Orientation::Horizontal)
    {
        visuals.line.Size({ width, 1.0f });
        visuals.line.CenterPoint({ width / 2.0f, 0.5f, 0.0f });
        lineOffset = { 0.0f, axis, 0.0f };
        visuals.rail.Size({ width, RailThickness });
        visuals.rail.CenterPoint({ width / 2.0f, RailThickness / 2.0f, 0.0f });
        railOffset = { 0.0f, axis - (RailThickness / 2.0f), 0.0f };
    }
    else
    {
        visuals.line.Size({ 1.0f, height });
        visuals.line.CenterPoint({ 0.5f, height / 2.0f, 0.0f });
        lineOffset = { axis, 0.0f, 0.0f };
        visuals.rail.Size({ RailThickness, height });
        visuals.rail.CenterPoint({ RailThickness / 2.0f, height / 2.0f, 0.0f });
        railOffset = { axis - (RailThickness / 2.0f), 0.0f, 0.0f };
    }

    visuals.line.Offset(lineOffset);
    visuals.rail.Offset(railOffset);
}

void GuideCompositionRenderer::SetRailVisible(GuideVisuals& visuals, bool visible)
{
    if (visuals.railVisible == visible)
    {
        return;
    }

    visuals.railVisible = visible;
    AnimateRail(visuals.rail, visuals.guide.orientation, visible);
}

void GuideCompositionRenderer::CreateOrMovePreview(const InteractionVisualState& state)
{
    const auto& interaction = state.interaction;
    if (!_preview || _preview->monitorId != interaction.monitor.id || _preview->orientation != interaction.orientation)
    {
        RemovePreview();
        auto* monitor = FindMonitor(interaction.monitor.id);
        if (!monitor)
        {
            return;
        }

        PreviewVisuals preview;
        preview.monitorId = interaction.monitor.id;
        preview.orientation = interaction.orientation;
        preview.rail = _compositor.CreateSpriteVisual();
        preview.rail.Brush(_railBrush);
        preview.line = _compositor.CreateSpriteVisual();
        preview.line.Brush(_lineBrush);
        monitor->interactionLayer.Children().InsertAtTop(preview.rail);
        monitor->interactionLayer.Children().InsertAtTop(preview.line);
        _preview = std::move(preview);

        AnimateOpacity(_preview->line, 0.0f, 1.0f, FastDuration);
        AnimateOpacity(_preview->rail, 0.0f, 0.7f, FastDuration);
    }

    const float width = static_cast<float>(interaction.monitor.bounds.right - interaction.monitor.bounds.left);
    const float height = static_cast<float>(interaction.monitor.bounds.bottom - interaction.monitor.bounds.top);
    const float axis = static_cast<float>(interaction.coordinate);
    winrt::float3 lineOffset;
    winrt::float3 railOffset;
    if (interaction.orientation == GuideModel::Orientation::Horizontal)
    {
        _preview->line.Size({ width, 1.0f });
        _preview->rail.Size({ width, RailThickness });
        lineOffset = { 0.0f, axis, 0.0f };
        railOffset = { 0.0f, axis - (RailThickness / 2.0f), 0.0f };
    }
    else
    {
        _preview->line.Size({ 1.0f, height });
        _preview->rail.Size({ RailThickness, height });
        lineOffset = { axis, 0.0f, 0.0f };
        railOffset = { axis - (RailThickness / 2.0f), 0.0f, 0.0f };
    }

    _preview->line.Offset(lineOffset);
    _preview->rail.Offset(railOffset);
}

void GuideCompositionRenderer::RemovePreview()
{
    if (!_preview)
    {
        return;
    }

    if (auto* monitor = FindMonitor(_preview->monitorId))
    {
        monitor->interactionLayer.Children().Remove(_preview->line);
        monitor->interactionLayer.Children().Remove(_preview->rail);
    }
    _preview.reset();
}

GuideCompositionRenderer::LabelVisuals GuideCompositionRenderer::CreateLabelVisual(
    const MonitorVisuals& monitor,
    bool removal,
    std::wstring_view text,
    bool axisPrefix)
{
    LabelVisuals label;
    label.monitorId = monitor.monitor.id;
    label.dpi = monitor.dpi;
    label.metrics = GuideVisualMetrics::LabelForDpi(monitor.dpi);
    label.removal = removal;
    label.axisPrefix = axisPrefix;
    label.darkMode = WindowsColors::is_dark_mode();
    label.highContrast = IsHighContrast();

    winrt::check_hresult(_dxgiAPI->writeFactory->CreateTextFormat(
        L"Segoe UI Variable Text",
        nullptr,
        DWRITE_FONT_WEIGHT_SEMI_BOLD,
        DWRITE_FONT_STYLE_NORMAL,
        DWRITE_FONT_STRETCH_NORMAL,
        label.metrics.fontSize,
        L"",
        label.textFormat.put()));
    winrt::check_hresult(label.textFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER));
    winrt::check_hresult(label.textFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_CENTER));
    winrt::check_hresult(label.textFormat->SetWordWrapping(DWRITE_WORD_WRAPPING_NO_WRAP));
    UpdateLabelText(label, text);

    label.surface = _graphicsDevice.CreateDrawingSurface(
        { label.metrics.width, label.metrics.height },
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
        winrt::Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
    label.brush = _compositor.CreateSurfaceBrush(label.surface);

    const float backgroundWidth = label.metrics.width - (2.0f * label.metrics.backgroundInset);
    const float backgroundHeight = label.metrics.height - (2.0f * label.metrics.backgroundInset);
    label.maskSurface = _graphicsDevice.CreateDrawingSurface(
        { backgroundWidth, backgroundHeight },
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
        winrt::Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
    label.maskBrush = _compositor.CreateSurfaceBrush(label.maskSurface);
    label.backgroundBrush = _compositor.CreateMaskBrush();
    label.backgroundBrush.Mask(label.maskBrush);
    label.backgroundBrush.Source(CreateLabelBackgroundBrush(removal));

    label.backgroundVisual = _compositor.CreateSpriteVisual();
    label.backgroundVisual.Size({ backgroundWidth, backgroundHeight });
    label.backgroundVisual.Offset({
        label.metrics.backgroundInset,
        label.metrics.backgroundInset,
        0.0f,
    });
    label.backgroundVisual.Brush(label.backgroundBrush);

    label.contentVisual = _compositor.CreateSpriteVisual();
    label.contentVisual.Size({ label.metrics.width, label.metrics.height });
    label.contentVisual.Brush(label.brush);

    label.visual = _compositor.CreateContainerVisual();
    label.visual.Size({ label.metrics.width, label.metrics.height });
    label.visual.CenterPoint({
        label.metrics.width / 2.0f,
        label.metrics.height / 2.0f,
        0.0f,
    });
    label.visual.Children().InsertAtTop(label.backgroundVisual);
    label.visual.Children().InsertAtTop(label.contentVisual);

    DrawLabelMask(label);
    return label;
}

void GuideCompositionRenderer::UpdateLabelText(LabelVisuals& label, std::wstring_view text)
{
    winrt::com_ptr<IDWriteTextLayout> textLayout;
    winrt::check_hresult(_dxgiAPI->writeFactory->CreateTextLayout(
        text.data(),
        static_cast<uint32_t>(text.size()),
        label.textFormat.get(),
        std::numeric_limits<float>::max(),
        std::numeric_limits<float>::max(),
        textLayout.put()));

    DWRITE_TEXT_METRICS textMetrics{};
    winrt::check_hresult(textLayout->GetMetrics(&textMetrics));
    const auto metrics = GuideVisualMetrics::SizeToContent(
        GuideVisualMetrics::LabelForDpi(label.dpi),
        std::max(textMetrics.width, textMetrics.widthIncludingTrailingWhitespace),
        textMetrics.height);
    const float contentWidth =
        metrics.width - (2.0f * (metrics.backgroundInset + metrics.horizontalTextInset));
    const float contentHeight =
        metrics.height - (2.0f * (metrics.backgroundInset + metrics.verticalTextInset));
    winrt::check_hresult(textLayout->SetMaxWidth(contentWidth));
    winrt::check_hresult(textLayout->SetMaxHeight(contentHeight));

    label.text.assign(text);
    label.textLayout = std::move(textLayout);
    ResizeLabelVisual(label, metrics);
}

void GuideCompositionRenderer::ResizeLabelVisual(
    LabelVisuals& label,
    const GuideVisualMetrics::LabelMetrics& metrics)
{
    const bool sizeChanged = label.metrics.width != metrics.width || label.metrics.height != metrics.height;
    label.metrics = metrics;
    if (!sizeChanged || !label.surface)
    {
        return;
    }

    const int32_t width = static_cast<int32_t>(metrics.width);
    const int32_t height = static_cast<int32_t>(metrics.height);
    const float backgroundWidth = metrics.width - (2.0f * metrics.backgroundInset);
    const float backgroundHeight = metrics.height - (2.0f * metrics.backgroundInset);
    label.surface.Resize({ width, height });
    label.maskSurface.Resize({
        static_cast<int32_t>(std::ceil(backgroundWidth)),
        static_cast<int32_t>(std::ceil(backgroundHeight)),
    });
    label.backgroundVisual.Size({ backgroundWidth, backgroundHeight });
    label.backgroundVisual.Offset({
        metrics.backgroundInset,
        metrics.backgroundInset,
        0.0f,
    });
    label.contentVisual.Size({ metrics.width, metrics.height });
    label.visual.Size({ metrics.width, metrics.height });
    label.visual.CenterPoint({
        metrics.width / 2.0f,
        metrics.height / 2.0f,
        0.0f,
    });
    DrawLabelMask(label);
    label.drawnText.clear();
}

void GuideCompositionRenderer::UpdateDistanceLabels()
{
    if (!_editMode)
    {
        ClearDistanceLabels();
        return;
    }

    auto effectiveGuides = _syncedGuides;
    if (_interaction)
    {
        const auto& interaction = _interaction->interaction;
        if (interaction.kind == GuideModel::InteractionKind::Placement)
        {
            if (!interaction.removeOnCommit)
            {
                effectiveGuides.push_back(GuideModel::Guide{
                    .orientation = interaction.orientation,
                    .monitorId = interaction.monitor.id,
                    .coordinate = interaction.coordinate,
                });
            }
        }
        else if (interaction.guideId)
        {
            const auto guide = std::ranges::find_if(effectiveGuides, [id = *interaction.guideId](const auto& item) {
                return item.id == id;
            });
            if (guide != effectiveGuides.end())
            {
                if (interaction.removeOnCommit)
                {
                    effectiveGuides.erase(guide);
                }
                else
                {
                    guide->orientation = interaction.orientation;
                    guide->monitorId = interaction.monitor.id;
                    guide->coordinate = interaction.coordinate;
                }
            }
        }
    }

    struct DesiredDistanceLabel
    {
        GuideModel::DistanceSegment segment;
        size_t ordinal = 0;
    };

    std::vector<DesiredDistanceLabel> desiredLabels;
    for (const auto& [_, monitor] : _monitors)
    {
        size_t horizontalOrdinal = 0;
        size_t verticalOrdinal = 0;
        for (const auto& segment : GuideModel::GetDistanceSegments(effectiveGuides, monitor.monitor))
        {
            auto& ordinal = segment.orientation == GuideModel::Orientation::Horizontal ?
                                horizontalOrdinal :
                                verticalOrdinal;
            desiredLabels.push_back(DesiredDistanceLabel{
                .segment = segment,
                .ordinal = ordinal++,
            });
        }
    }

    bool redrawNeeded = false;
    std::vector<DistanceLabelVisuals> updatedLabels;
    updatedLabels.reserve(desiredLabels.size());
    for (const auto& desired : desiredLabels)
    {
        auto* monitor = FindMonitor(desired.segment.monitorId);
        if (!monitor)
        {
            continue;
        }

        const std::wstring text =
            std::to_wstring(desired.segment.Length()) + L" " +
            Measurement::GetUnitAbbreviation(Measurement::Unit::Pixel);
        const auto existing = std::ranges::find_if(_distanceLabels, [&desired](const auto& item) {
            return item.monitorId == desired.segment.monitorId &&
                   item.orientation == desired.segment.orientation &&
                   item.ordinal == desired.ordinal;
        });

        const bool alreadyAttached = existing != _distanceLabels.end();
        DistanceLabelVisuals visuals;
        if (alreadyAttached)
        {
            visuals = std::move(*existing);
            _distanceLabels.erase(existing);
        }
        else
        {
            visuals.monitorId = desired.segment.monitorId;
            visuals.orientation = desired.segment.orientation;
            visuals.ordinal = desired.ordinal;
            visuals.label = CreateLabelVisual(*monitor, false, text, false);
            redrawNeeded = true;
        }

        if (visuals.label.text != text)
        {
            UpdateLabelText(visuals.label, text);
            redrawNeeded = true;
        }

        const bool darkMode = WindowsColors::is_dark_mode();
        const bool highContrast = IsHighContrast();
        if (visuals.label.darkMode != darkMode || visuals.label.highContrast != highContrast)
        {
            visuals.label.backgroundBrush.Source(CreateLabelBackgroundBrush(false));
            visuals.label.darkMode = darkMode;
            visuals.label.highContrast = highContrast;
            visuals.label.drawnText.clear();
            redrawNeeded = true;
        }

        const float requiredLength =
            desired.segment.orientation == GuideModel::Orientation::Vertical ?
                visuals.label.metrics.width :
                visuals.label.metrics.height;
        if (static_cast<float>(desired.segment.Length()) <
            requiredLength + (2.0f * visuals.label.metrics.screenMargin))
        {
            if (alreadyAttached)
            {
                monitor->distanceLabelLayer.Children().Remove(visuals.label.visual);
            }
            continue;
        }

        visuals.segment = desired.segment;
        PositionDistanceLabel(visuals, desired.segment);
        if (!alreadyAttached)
        {
            monitor->distanceLabelLayer.Children().InsertAtTop(visuals.label.visual);
        }
        updatedLabels.push_back(std::move(visuals));
    }

    for (auto& obsolete : _distanceLabels)
    {
        if (auto* monitor = FindMonitor(obsolete.monitorId))
        {
            monitor->distanceLabelLayer.Children().Remove(obsolete.label.visual);
        }
    }
    _distanceLabels = std::move(updatedLabels);

    if (redrawNeeded)
    {
        ScheduleLabelRedraw();
    }
}

void GuideCompositionRenderer::ClearDistanceLabels()
{
    for (auto& distanceLabel : _distanceLabels)
    {
        if (auto* monitor = FindMonitor(distanceLabel.monitorId))
        {
            monitor->distanceLabelLayer.Children().Remove(distanceLabel.label.visual);
        }
    }
    _distanceLabels.clear();
}

void GuideCompositionRenderer::PositionDistanceLabel(
    DistanceLabelVisuals& visuals,
    const GuideModel::DistanceSegment& segment)
{
    const auto* monitor = FindMonitor(segment.monitorId);
    if (!monitor)
    {
        return;
    }

    const auto& metrics = visuals.label.metrics;
    const float workAreaLeft = static_cast<float>(monitor->workArea.left - monitor->monitor.bounds.left);
    const float workAreaTop = static_cast<float>(monitor->workArea.top - monitor->monitor.bounds.top);
    const float workAreaRight = static_cast<float>(monitor->workArea.right - monitor->monitor.bounds.left);
    const float workAreaBottom = static_cast<float>(monitor->workArea.bottom - monitor->monitor.bounds.top);
    const float midpoint =
        (static_cast<float>(segment.startCoordinate) + static_cast<float>(segment.endCoordinate)) / 2.0f;
    const float edgeOffset = metrics.screenMargin + metrics.guideOffset;
    float x = workAreaLeft + edgeOffset;
    float y = workAreaTop + edgeOffset;
    if (segment.orientation == GuideModel::Orientation::Vertical)
    {
        x = midpoint - (metrics.width / 2.0f);
        const int topBandBottom =
            monitor->workArea.top + static_cast<int>(std::ceil((2.0f * edgeOffset) + metrics.height));
        const bool toolbarAtTop =
            _toolbarBounds.right > monitor->workArea.left &&
            _toolbarBounds.left < monitor->workArea.right &&
            _toolbarBounds.bottom > monitor->workArea.top &&
            _toolbarBounds.top < topBandBottom;
        if (toolbarAtTop)
        {
            y = workAreaBottom - edgeOffset - metrics.height;
        }
    }
    else
    {
        y = midpoint - (metrics.height / 2.0f);
        const int leftBandRight =
            monitor->workArea.left + static_cast<int>(std::ceil((2.0f * edgeOffset) + metrics.width));
        const bool toolbarAtLeft =
            _toolbarBounds.bottom > monitor->workArea.top &&
            _toolbarBounds.top < monitor->workArea.bottom &&
            _toolbarBounds.right > monitor->workArea.left &&
            _toolbarBounds.left < leftBandRight;
        if (toolbarAtLeft)
        {
            x = workAreaRight - edgeOffset - metrics.width;
        }
    }

    visuals.label.visual.Offset({
        std::round(x),
        std::round(y),
        0.0f,
    });
}

void GuideCompositionRenderer::ShowLabel(const InteractionVisualState& state)
{
    const auto& interaction = state.interaction;
    auto* monitor = FindMonitor(interaction.monitor.id);
    if (!monitor)
    {
        return;
    }

    const bool removing = state.dismissalEdge != GuideModel::DismissalEdge::None;
    std::wstring text;
    if (removing)
    {
        text = Measurement::removeGuideLabel.c_str();
    }
    else
    {
        text = interaction.orientation == GuideModel::Orientation::Horizontal ? L"Y " : L"X ";
        text += std::to_wstring(interaction.coordinate);
        text += L" ";
        text += Measurement::GetUnitAbbreviation(Measurement::Unit::Pixel);
    }

    const bool monitorChanged = _label && _label->monitorId != interaction.monitor.id;
    if (_label && _label->dpi != monitor->dpi)
    {
        HideLabel();
    }
    bool labelCreated = false;
    if (!_label)
    {
        _label = CreateLabelVisual(*monitor, removing, text, !removing);
        monitor->labelRoot.Children().InsertAtTop(_label->visual);
        labelCreated = true;
    }
    else if (monitorChanged)
    {
        if (auto* oldMonitor = FindMonitor(_label->monitorId))
        {
            oldMonitor->labelRoot.Children().Remove(_label->visual);
            HideLabelWindow(*oldMonitor);
        }
        monitor->labelRoot.Children().InsertAtTop(_label->visual);
        _label->monitorId = interaction.monitor.id;
    }

    const bool darkMode = WindowsColors::is_dark_mode();
    const bool highContrast = IsHighContrast();
    const bool textChanged = _label->text != text;
    const bool appearanceChanged = _label->removal != removing;
    const bool axisPrefixChanged = _label->axisPrefix != !removing;
    const bool themeChanged = _label->darkMode != darkMode || _label->highContrast != highContrast;
    if (appearanceChanged || monitorChanged || themeChanged)
    {
        _label->backgroundBrush.Source(CreateLabelBackgroundBrush(removing));
    }
    if (textChanged)
    {
        UpdateLabelText(*_label, text);
    }
    _label->removal = removing;
    _label->axisPrefix = !removing;
    _label->darkMode = darkMode;
    _label->highContrast = highContrast;
    if (themeChanged || axisPrefixChanged)
    {
        _label->drawnText.clear();
    }
    if (labelCreated || textChanged || appearanceChanged || axisPrefixChanged || themeChanged)
    {
        ScheduleLabelRedraw();
    }

    const auto& metrics = _label->metrics;
    const float monitorWidth = static_cast<float>(interaction.monitor.bounds.right - interaction.monitor.bounds.left);
    const float monitorHeight = static_cast<float>(interaction.monitor.bounds.bottom - interaction.monitor.bounds.top);
    const float pointerX = static_cast<float>(state.systemPointer.x - interaction.monitor.bounds.left);
    const float pointerY = static_cast<float>(state.systemPointer.y - interaction.monitor.bounds.top);
    float x = pointerX + metrics.pointerOffset;
    float y = pointerY + metrics.pointerOffset;
    if (interaction.orientation == GuideModel::Orientation::Horizontal)
    {
        y = static_cast<float>(interaction.coordinate) + metrics.guideOffset;
    }
    else
    {
        x = static_cast<float>(interaction.coordinate) + metrics.guideOffset;
    }

    x = std::clamp(
        x,
        metrics.screenMargin,
        std::max(metrics.screenMargin, monitorWidth - metrics.width - metrics.screenMargin));
    y = std::clamp(
        y,
        metrics.screenMargin,
        std::max(metrics.screenMargin, monitorHeight - metrics.height - metrics.screenMargin));
    _label->visual.Offset({ 0.0f, 0.0f, 0.0f });
    PositionLabelWindow(*monitor, x, y, metrics);

    if (!_label->shown)
    {
        _label->shown = true;
        _label->visual.Opacity(1.0f);
        _label->visual.Scale({ 1.0f, 1.0f, 1.0f });
    }
}

void GuideCompositionRenderer::HideLabel()
{
    if (!_label)
    {
        return;
    }

    if (auto* monitor = FindMonitor(_label->monitorId))
    {
        monitor->labelRoot.Children().Remove(_label->visual);
        HideLabelWindow(*monitor);
    }
    _label.reset();
}

void GuideCompositionRenderer::ScheduleLabelRedraw()
{
    if (_labelRedrawScheduled)
    {
        return;
    }
    if (!_labelRedrawTimer)
    {
        DrawPendingLabels();
        return;
    }

    _labelRedrawScheduled = true;
    _labelRedrawTimer.Start();
}

void GuideCompositionRenderer::DrawPendingLabels()
{
    const auto drawIfNeeded = [this](LabelVisuals& label) {
        if (label.drawnText == label.text &&
            label.drawnRemoval == label.removal &&
            label.drawnAxisPrefix == label.axisPrefix)
        {
            return;
        }

        if (DrawLabel(label))
        {
            label.drawnText = label.text;
            label.drawnRemoval = label.removal;
            label.drawnAxisPrefix = label.axisPrefix;
        }
    };

    if (_label)
    {
        drawIfNeeded(*_label);
    }
    for (auto& distanceLabel : _distanceLabels)
    {
        drawIfNeeded(distanceLabel.label);
    }
}

bool GuideCompositionRenderer::DrawLabel(LabelVisuals& label)
{
    if (!label.textLayout)
    {
        return false;
    }

    auto surfaceInterop = label.surface.as<ABI::Windows::UI::Composition::ICompositionDrawingSurfaceInterop>();
    winrt::com_ptr<ID2D1DeviceContext> context;
    POINT offset{};
    const HRESULT beginResult = surfaceInterop->BeginDraw(
        nullptr,
        __uuidof(ID2D1DeviceContext),
        context.put_void(),
        &offset);
    if (beginResult == DXGI_ERROR_DEVICE_REMOVED || beginResult == DXGI_ERROR_DEVICE_RESET)
    {
        return false;
    }
    winrt::check_hresult(beginResult);
    bool drawEnded = false;
    const auto endDraw = wil::scope_exit([&] {
        if (!drawEnded)
        {
            LOG_IF_FAILED(surfaceInterop->EndDraw());
        }
    });

    context->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x), static_cast<float>(offset.y)));
    context->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));

    const bool removal = label.removal;
    const auto palette = MeasurementTooltipStyle::PaletteForTheme(WindowsColors::is_dark_mode());
    D2D1_COLOR_F foreground = D2DColor(palette.foreground);
    D2D1_COLOR_F secondaryForeground = D2DColor(palette.secondaryForeground);
    D2D1_COLOR_F border = D2DColor(palette.border);
    if (IsHighContrast())
    {
        const auto foregroundColor = SystemColor(removal ? COLOR_HIGHLIGHTTEXT : COLOR_WINDOWTEXT);
        foreground = D2D1::ColorF(
            foregroundColor.R / 255.0f,
            foregroundColor.G / 255.0f,
            foregroundColor.B / 255.0f,
            1.0f);
        secondaryForeground = foreground;
        border = foreground;
    }

    winrt::com_ptr<ID2D1SolidColorBrush> foregroundBrush;
    winrt::com_ptr<ID2D1SolidColorBrush> secondaryForegroundBrush;
    winrt::com_ptr<ID2D1SolidColorBrush> borderBrush;
    winrt::check_hresult(context->CreateSolidColorBrush(foreground, foregroundBrush.put()));
    winrt::check_hresult(context->CreateSolidColorBrush(secondaryForeground, secondaryForegroundBrush.put()));
    winrt::check_hresult(context->CreateSolidColorBrush(border, borderBrush.put()));

    const auto& metrics = label.metrics;
    const float halfBorder = metrics.borderThickness / 2.0f;
    const D2D1_ROUNDED_RECT borderRect{
        D2D1::RectF(
            halfBorder,
            halfBorder,
            metrics.width - halfBorder,
            metrics.height - halfBorder),
        std::max(0.0f, metrics.cornerRadius - halfBorder),
        std::max(0.0f, metrics.cornerRadius - halfBorder),
    };
    context->DrawRoundedRectangle(borderRect, borderBrush.get(), metrics.borderThickness);

    const DWRITE_TEXT_RANGE axisRange{
        0,
        static_cast<uint32_t>(std::min<size_t>(2, label.text.size())),
    };
    winrt::check_hresult(label.textLayout->SetDrawingEffect(
        label.axisPrefix ? secondaryForegroundBrush.get() : foregroundBrush.get(),
        axisRange));
    context->DrawTextLayout(
        D2D1::Point2F(
            metrics.backgroundInset + metrics.horizontalTextInset,
            metrics.backgroundInset + metrics.verticalTextInset),
        label.textLayout.get(),
        foregroundBrush.get());

    const HRESULT endDrawResult = surfaceInterop->EndDraw();
    drawEnded = true;
    winrt::check_hresult(endDrawResult);
    return true;
}

bool GuideCompositionRenderer::DrawLabelMask(LabelVisuals& label)
{
    auto surfaceInterop = label.maskSurface.as<ABI::Windows::UI::Composition::ICompositionDrawingSurfaceInterop>();
    winrt::com_ptr<ID2D1DeviceContext> context;
    POINT offset{};
    const HRESULT beginResult = surfaceInterop->BeginDraw(
        nullptr,
        __uuidof(ID2D1DeviceContext),
        context.put_void(),
        &offset);
    if (beginResult == DXGI_ERROR_DEVICE_REMOVED || beginResult == DXGI_ERROR_DEVICE_RESET)
    {
        return false;
    }
    winrt::check_hresult(beginResult);
    bool drawEnded = false;
    const auto endDraw = wil::scope_exit([&] {
        if (!drawEnded)
        {
            LOG_IF_FAILED(surfaceInterop->EndDraw());
        }
    });

    context->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<float>(offset.x), static_cast<float>(offset.y)));
    context->Clear(D2D1::ColorF(0.0f, 0.0f, 0.0f, 0.0f));
    winrt::com_ptr<ID2D1SolidColorBrush> maskBrush;
    winrt::check_hresult(context->CreateSolidColorBrush(D2D1::ColorF(D2D1::ColorF::White), maskBrush.put()));

    const float width = label.metrics.width - (2.0f * label.metrics.backgroundInset);
    const float height = label.metrics.height - (2.0f * label.metrics.backgroundInset);
    const D2D1_ROUNDED_RECT maskRect{
        D2D1::RectF(0.0f, 0.0f, width, height),
        label.metrics.cornerRadius,
        label.metrics.cornerRadius,
    };
    context->FillRoundedRectangle(maskRect, maskBrush.get());

    const HRESULT endDrawResult = surfaceInterop->EndDraw();
    drawEnded = true;
    winrt::check_hresult(endDrawResult);
    return true;
}

muxc::CompositionBrush GuideCompositionRenderer::CreateLabelBackgroundBrush(bool removal)
{
    if (IsHighContrast())
    {
        return _compositor.CreateColorBrush(SystemColor(removal ? COLOR_HIGHLIGHT : COLOR_WINDOW));
    }
    const auto palette = MeasurementTooltipStyle::PaletteForTheme(WindowsColors::is_dark_mode());
    return _compositor.CreateColorBrush(CompositionColor(palette.background));
}

void GuideCompositionRenderer::PositionLabelWindow(
    MonitorVisuals& monitor,
    float x,
    float y,
    const GuideVisualMetrics::LabelMetrics& metrics)
{
    const int width = static_cast<int>(std::ceil(metrics.width));
    const int height = static_cast<int>(std::ceil(metrics.height));
    const bool sizeChanged = monitor.labelSize.cx != width || monitor.labelSize.cy != height;
    if (sizeChanged)
    {
        const int cornerDiameter = static_cast<int>(std::ceil(metrics.cornerRadius * 2.0f));
        wil::unique_hrgn region{ CreateRoundRectRgn(0, 0, width + 1, height + 1, cornerDiameter, cornerDiameter) };
        if (region)
        {
            if (SetWindowRgn(monitor.labelWindow, region.get(), FALSE))
            {
                region.release();
            }
            else
            {
                Logger::warn(L"Failed to set the Screen Ruler guide label window region");
            }
        }
        monitor.labelSize = { width, height };
    }

    SetWindowPos(
        monitor.labelWindow,
        HWND_TOPMOST,
        monitor.monitor.bounds.left + static_cast<int>(std::round(x)),
        monitor.monitor.bounds.top + static_cast<int>(std::round(y)),
        width,
        height,
        SWP_NOACTIVATE | SWP_SHOWWINDOW | (sizeChanged ? 0 : SWP_NOSIZE));
}

void GuideCompositionRenderer::HideLabelWindow(MonitorVisuals& monitor)
{
    ShowWindow(monitor.labelWindow, SW_HIDE);
}

void GuideCompositionRenderer::ShowSnapPulse(const InteractionVisualState& state)
{
    auto* monitor = FindMonitor(state.interaction.monitor.id);
    if (!monitor || !AnimationsEnabled())
    {
        return;
    }

    auto marker = _compositor.CreateSpriteVisual();
    marker.Size({ 10.0f, 10.0f });
    marker.CenterPoint({ 5.0f, 5.0f, 0.0f });
    marker.Brush(_lineBrush);
    const float pointerX = static_cast<float>(state.systemPointer.x - state.interaction.monitor.bounds.left);
    const float pointerY = static_cast<float>(state.systemPointer.y - state.interaction.monitor.bounds.top);
    marker.Offset({
        state.interaction.orientation == GuideModel::Orientation::Vertical ? static_cast<float>(state.interaction.coordinate) - 5.0f : pointerX - 5.0f,
        state.interaction.orientation == GuideModel::Orientation::Horizontal ? static_cast<float>(state.interaction.coordinate) - 5.0f : pointerY - 5.0f,
        0.0f,
    });
    monitor->interactionLayer.Children().InsertAtTop(marker);
    auto batch = _compositor.CreateScopedBatch(muxc::CompositionBatchTypes::Animation);

    auto opacity = _compositor.CreateScalarKeyFrameAnimation();
    opacity.InsertKeyFrame(0.0f, 0.9f);
    opacity.InsertKeyFrame(1.0f, 0.0f);
    opacity.Duration(NormalDuration);
    marker.StartAnimation(L"Opacity", opacity);

    auto scale = _compositor.CreateVector3KeyFrameAnimation();
    scale.InsertKeyFrame(0.0f, { 0.45f, 0.45f, 1.0f });
    scale.InsertKeyFrame(1.0f, { 1.8f, 1.8f, 1.0f });
    scale.Duration(NormalDuration);
    marker.StartAnimation(L"Scale", scale);

    const auto layer = monitor->interactionLayer;
    batch.Completed([layer, marker](auto&&, auto&&) {
        layer.Children().Remove(marker);
    });
    batch.End();
}

void GuideCompositionRenderer::UpdateRemovalAffordance(const InteractionVisualState& state)
{
    if (state.dismissalEdge == GuideModel::DismissalEdge::None)
    {
        HideRemovalAffordance();
        return;
    }

    if (_removal && _removal->monitorId == state.interaction.monitor.id && _removal->edge == state.dismissalEdge)
    {
        return;
    }

    HideRemovalAffordance();
    auto* monitor = FindMonitor(state.interaction.monitor.id);
    if (!monitor)
    {
        return;
    }

    RemovalVisuals removal;
    removal.monitorId = state.interaction.monitor.id;
    removal.edge = state.dismissalEdge;
    removal.visual = _compositor.CreateSpriteVisual();
    removal.visual.Brush(_removalBrush);

    const float width = static_cast<float>(state.interaction.monitor.bounds.right - state.interaction.monitor.bounds.left);
    const float height = static_cast<float>(state.interaction.monitor.bounds.bottom - state.interaction.monitor.bounds.top);
    switch (state.dismissalEdge)
    {
    case GuideModel::DismissalEdge::Left:
        removal.visual.Size({ RemovalThickness, height });
        removal.visual.Offset({ 0.0f, 0.0f, 0.0f });
        break;
    case GuideModel::DismissalEdge::Right:
        removal.visual.Size({ RemovalThickness, height });
        removal.visual.Offset({ width - RemovalThickness, 0.0f, 0.0f });
        break;
    case GuideModel::DismissalEdge::Top:
        removal.visual.Size({ width, RemovalThickness });
        removal.visual.Offset({ 0.0f, 0.0f, 0.0f });
        break;
    case GuideModel::DismissalEdge::Bottom:
        removal.visual.Size({ width, RemovalThickness });
        removal.visual.Offset({ 0.0f, height - RemovalThickness, 0.0f });
        break;
    case GuideModel::DismissalEdge::None:
        return;
    }

    monitor->interactionLayer.Children().InsertAtTop(removal.visual);
    AnimateOpacity(removal.visual, 0.0f, 0.65f, FastDuration);
    _removal = std::move(removal);
}

void GuideCompositionRenderer::HideRemovalAffordance()
{
    if (!_removal)
    {
        return;
    }

    if (auto* monitor = FindMonitor(_removal->monitorId))
    {
        monitor->interactionLayer.Children().Remove(_removal->visual);
    }
    _removal.reset();
}

void GuideCompositionRenderer::AnimateOpacity(
    const muxc::Visual& visual,
    float from,
    float to,
    std::chrono::milliseconds duration)
{
    visual.StopAnimation(L"Opacity");
    visual.Opacity(to);
    if (!AnimationsEnabled())
    {
        return;
    }

    auto animation = _compositor.CreateScalarKeyFrameAnimation();
    animation.InsertKeyFrame(0.0f, from);
    animation.InsertKeyFrame(1.0f, to);
    animation.Duration(duration);
    visual.StartAnimation(L"Opacity", animation);
}

void GuideCompositionRenderer::AnimateRail(
    const muxc::SpriteVisual& rail,
    GuideModel::Orientation orientation,
    bool visible)
{
    const auto finalScale = orientation == GuideModel::Orientation::Horizontal ?
                                winrt::float3{ 1.0f, visible ? 1.0f : 0.2f, 1.0f } :
                                winrt::float3{ visible ? 1.0f : 0.2f, 1.0f, 1.0f };
    if (!AnimationsEnabled())
    {
        rail.Opacity(visible ? 0.7f : 0.0f);
        rail.Scale(finalScale);
        return;
    }

    AnimateOpacity(rail, rail.Opacity(), visible ? 0.7f : 0.0f, FastDuration);
    auto scale = _compositor.CreateVector3KeyFrameAnimation();
    scale.InsertKeyFrame(1.0f, finalScale);
    scale.Duration(FastDuration);
    rail.StartAnimation(L"Scale", scale);
}

winrt::Windows::UI::Color GuideCompositionRenderer::ToColor(D2D1::ColorF color, uint8_t alpha) const
{
    if (IsHighContrast())
    {
        return SystemColor(COLOR_HIGHLIGHT);
    }

    return winrt::Windows::UI::ColorHelper::FromArgb(
        alpha,
        static_cast<uint8_t>(std::clamp(color.r, 0.0f, 1.0f) * 255.0f),
        static_cast<uint8_t>(std::clamp(color.g, 0.0f, 1.0f) * 255.0f),
        static_cast<uint8_t>(std::clamp(color.b, 0.0f, 1.0f) * 255.0f));
}

bool GuideCompositionRenderer::AnimationsEnabled() const
{
    return GetAnimationsEnabled();
}
