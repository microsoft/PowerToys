#pragma once

#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>

#include <cstdint>
#include <optional>
#include <vector>

namespace GuideModel
{
    using GuideId = uint64_t;
    using MonitorId = uintptr_t;

    enum class Orientation
    {
        Horizontal,
        Vertical,
    };

    enum class DismissalEdge
    {
        None,
        Left,
        Top,
        Right,
        Bottom,
    };

    struct Monitor
    {
        MonitorId id = 0;
        RECT bounds{};
    };

    struct Guide
    {
        GuideId id = 0;
        Orientation orientation = Orientation::Horizontal;
        MonitorId monitorId = 0;
        int coordinate = 0;
    };

    struct DistanceSegment
    {
        Orientation orientation = Orientation::Horizontal;
        MonitorId monitorId = 0;
        int startCoordinate = 0;
        int endCoordinate = 0;

        int Length() const noexcept
        {
            return endCoordinate - startCoordinate;
        }
    };

    struct HitTestResult
    {
        GuideId id = 0;
        int distance = 0;
    };

    int AxisLength(Orientation orientation, const RECT& monitorBounds);
    int ClampCoordinate(Orientation orientation, const RECT& monitorBounds, int coordinate);
    int ToMonitorCoordinate(Orientation orientation, const RECT& monitorBounds, POINT systemPoint);
    int ToSystemCoordinate(const Guide& guide, const RECT& monitorBounds);
    int NearestAxisEdge(Orientation orientation, const RECT& detectedEdges, int rawCoordinate);
    std::vector<DistanceSegment> GetDistanceSegments(
        const std::vector<Guide>& guides,
        const Monitor& monitor);

    class MagneticSnapController
    {
    public:
        explicit MagneticSnapController(int acquisitionDistance = 8, int releaseDistance = 14);

        std::optional<int> Track(int rawCoordinate, bool bypass);
        std::optional<int> UpdateCandidate(int rawCoordinate, int candidateCoordinate, bool bypass);
        void Reset();

    private:
        int _acquisitionDistance = 0;
        int _releaseDistance = 0;
        std::optional<int> _target;
    };

    bool IsEdgeExposed(DismissalEdge edge, const Monitor& monitor, const std::vector<Monitor>& monitors);
    DismissalEdge GetDismissalEdge(
        Orientation orientation,
        POINT systemPoint,
        const Monitor& monitor,
        const std::vector<Monitor>& monitors,
        int threshold);

    class Collection
    {
    public:
        GuideId Add(Orientation orientation, const Monitor& monitor, int coordinate);
        bool Remove(GuideId id);
        void Clear();
        bool Move(GuideId id, const Monitor& monitor, int coordinate);

        Guide* Find(GuideId id);
        const Guide* Find(GuideId id) const;
        const std::vector<Guide>& Guides() const;
        bool Empty() const;

        std::optional<HitTestResult> HitTest(
            POINT systemPoint,
            const Monitor& monitor,
            int hitRadius) const;

    private:
        GuideId _nextId = 1;
        std::vector<Guide> _guides;
    };

    enum class InteractionKind
    {
        None,
        Placement,
        Drag,
    };

    struct Interaction
    {
        InteractionKind kind = InteractionKind::None;
        Orientation orientation = Orientation::Horizontal;
        std::optional<GuideId> guideId;
        Monitor monitor;
        int coordinate = 0;
        bool removeOnCommit = false;
    };

    class InteractionController
    {
    public:
        explicit InteractionController(Collection& collection);

        void BeginPlacement(Orientation orientation, const Monitor& monitor, int coordinate);
        bool BeginDrag(GuideId id, const Monitor& monitor);
        void Update(const Monitor& monitor, int coordinate, bool removeOnCommit);
        std::optional<GuideId> Commit();
        void Cancel();

        const Interaction& Current() const;
        bool Active() const;

    private:
        Collection& _collection;
        Interaction _interaction;
    };
}
