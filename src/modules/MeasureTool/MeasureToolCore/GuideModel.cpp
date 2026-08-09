#include "GuideModel.h"

#include <algorithm>
#include <cstdlib>
#include <limits>

namespace GuideModel
{
    namespace
    {
        constexpr bool IntervalsOverlap(int firstStart, int firstEnd, int secondStart, int secondEnd)
        {
            return std::max(firstStart, secondStart) < std::min(firstEnd, secondEnd);
        }

        int DistanceToGuide(const Guide& guide, POINT point, const RECT& monitorBounds)
        {
            const int pointCoordinate = guide.orientation == Orientation::Horizontal ? point.y : point.x;
            return std::abs(pointCoordinate - ToSystemCoordinate(guide, monitorBounds));
        }
    }

    int AxisLength(Orientation orientation, const RECT& monitorBounds)
    {
        return orientation == Orientation::Horizontal ?
                   monitorBounds.bottom - monitorBounds.top :
                   monitorBounds.right - monitorBounds.left;
    }

    int ClampCoordinate(Orientation orientation, const RECT& monitorBounds, int coordinate)
    {
        return std::clamp(coordinate, 0, std::max(0, AxisLength(orientation, monitorBounds) - 1));
    }

    int ToMonitorCoordinate(Orientation orientation, const RECT& monitorBounds, POINT systemPoint)
    {
        const int coordinate = orientation == Orientation::Horizontal ?
                                   systemPoint.y - monitorBounds.top :
                                   systemPoint.x - monitorBounds.left;
        return ClampCoordinate(orientation, monitorBounds, coordinate);
    }

    int ToSystemCoordinate(const Guide& guide, const RECT& monitorBounds)
    {
        return guide.coordinate + (guide.orientation == Orientation::Horizontal ? monitorBounds.top : monitorBounds.left);
    }

    int NearestAxisEdge(Orientation orientation, const RECT& detectedEdges, int rawCoordinate)
    {
        const int first = orientation == Orientation::Horizontal ? detectedEdges.top : detectedEdges.left;
        const int second = orientation == Orientation::Horizontal ? detectedEdges.bottom : detectedEdges.right;
        return std::abs(rawCoordinate - first) <= std::abs(rawCoordinate - second) ? first : second;
    }

    std::vector<DistanceSegment> GetDistanceSegments(
        const std::vector<Guide>& guides,
        const Monitor& monitor)
    {
        std::vector<DistanceSegment> segments;
        for (const auto orientation : { Orientation::Vertical, Orientation::Horizontal })
        {
            std::vector<int> coordinates;
            for (const auto& guide : guides)
            {
                if (guide.monitorId == monitor.id && guide.orientation == orientation)
                {
                    coordinates.push_back(ClampCoordinate(orientation, monitor.bounds, guide.coordinate));
                }
            }
            if (coordinates.empty())
            {
                continue;
            }

            std::ranges::sort(coordinates);
            const auto uniqueEnd = std::ranges::unique(coordinates).begin();
            coordinates.erase(uniqueEnd, coordinates.end());

            int startCoordinate = 0;
            for (const int coordinate : coordinates)
            {
                if (coordinate > startCoordinate)
                {
                    segments.push_back(DistanceSegment{
                        .orientation = orientation,
                        .monitorId = monitor.id,
                        .startCoordinate = startCoordinate,
                        .endCoordinate = coordinate,
                    });
                }
                startCoordinate = coordinate;
            }

            const int axisLength = AxisLength(orientation, monitor.bounds);
            if (startCoordinate < axisLength)
            {
                segments.push_back(DistanceSegment{
                    .orientation = orientation,
                    .monitorId = monitor.id,
                    .startCoordinate = startCoordinate,
                    .endCoordinate = axisLength,
                });
            }
        }
        return segments;
    }

    MagneticSnapController::MagneticSnapController(int acquisitionDistance, int releaseDistance) :
        _acquisitionDistance{ std::max(0, acquisitionDistance) },
        _releaseDistance{ std::max(_acquisitionDistance, releaseDistance) }
    {
    }

    std::optional<int> MagneticSnapController::Track(int rawCoordinate, bool bypass)
    {
        if (bypass)
        {
            Reset();
            return std::nullopt;
        }

        if (_target && std::abs(rawCoordinate - *_target) > _releaseDistance)
        {
            _target.reset();
        }
        return _target;
    }

    std::optional<int> MagneticSnapController::UpdateCandidate(
        int rawCoordinate,
        int candidateCoordinate,
        bool bypass)
    {
        if (bypass)
        {
            Reset();
            return std::nullopt;
        }

        if (const auto target = Track(rawCoordinate, false))
        {
            return target;
        }

        if (std::abs(rawCoordinate - candidateCoordinate) <= _acquisitionDistance)
        {
            _target = candidateCoordinate;
        }
        return _target;
    }

    void MagneticSnapController::Reset()
    {
        _target.reset();
    }

    bool IsEdgeExposed(DismissalEdge edge, const Monitor& monitor, const std::vector<Monitor>& monitors)
    {
        for (const auto& candidate : monitors)
        {
            if (candidate.id == monitor.id)
            {
                continue;
            }

            switch (edge)
            {
            case DismissalEdge::Left:
                if (candidate.bounds.right == monitor.bounds.left &&
                    IntervalsOverlap(candidate.bounds.top, candidate.bounds.bottom, monitor.bounds.top, monitor.bounds.bottom))
                {
                    return false;
                }
                break;

            case DismissalEdge::Right:
                if (candidate.bounds.left == monitor.bounds.right &&
                    IntervalsOverlap(candidate.bounds.top, candidate.bounds.bottom, monitor.bounds.top, monitor.bounds.bottom))
                {
                    return false;
                }
                break;

            case DismissalEdge::Top:
                if (candidate.bounds.bottom == monitor.bounds.top &&
                    IntervalsOverlap(candidate.bounds.left, candidate.bounds.right, monitor.bounds.left, monitor.bounds.right))
                {
                    return false;
                }
                break;

            case DismissalEdge::Bottom:
                if (candidate.bounds.top == monitor.bounds.bottom &&
                    IntervalsOverlap(candidate.bounds.left, candidate.bounds.right, monitor.bounds.left, monitor.bounds.right))
                {
                    return false;
                }
                break;

            case DismissalEdge::None:
                return false;
            }
        }

        return edge != DismissalEdge::None;
    }

    DismissalEdge GetDismissalEdge(
        Orientation orientation,
        POINT systemPoint,
        const Monitor& monitor,
        const std::vector<Monitor>& monitors,
        int threshold)
    {
        threshold = std::max(0, threshold);

        const auto firstEdge = orientation == Orientation::Horizontal ? DismissalEdge::Top : DismissalEdge::Left;
        const auto secondEdge = orientation == Orientation::Horizontal ? DismissalEdge::Bottom : DismissalEdge::Right;
        const int firstDistance = orientation == Orientation::Horizontal ?
                                      std::abs(systemPoint.y - monitor.bounds.top) :
                                      std::abs(systemPoint.x - monitor.bounds.left);
        const int secondDistance = orientation == Orientation::Horizontal ?
                                       std::abs(systemPoint.y - (monitor.bounds.bottom - 1)) :
                                       std::abs(systemPoint.x - (monitor.bounds.right - 1));

        if (firstDistance <= threshold && firstDistance <= secondDistance && IsEdgeExposed(firstEdge, monitor, monitors))
        {
            return firstEdge;
        }

        if (secondDistance <= threshold && IsEdgeExposed(secondEdge, monitor, monitors))
        {
            return secondEdge;
        }

        return DismissalEdge::None;
    }

    GuideId Collection::Add(Orientation orientation, const Monitor& monitor, int coordinate)
    {
        const GuideId id = _nextId++;
        _guides.push_back(Guide{
            .id = id,
            .orientation = orientation,
            .monitorId = monitor.id,
            .coordinate = ClampCoordinate(orientation, monitor.bounds, coordinate),
        });
        return id;
    }

    bool Collection::Remove(GuideId id)
    {
        const auto iterator = std::find_if(_guides.begin(), _guides.end(), [id](const Guide& guide) {
            return guide.id == id;
        });
        if (iterator == _guides.end())
        {
            return false;
        }

        _guides.erase(iterator);
        return true;
    }

    void Collection::Clear()
    {
        _guides.clear();
    }

    bool Collection::Move(GuideId id, const Monitor& monitor, int coordinate)
    {
        auto* guide = Find(id);
        if (!guide)
        {
            return false;
        }

        guide->monitorId = monitor.id;
        guide->coordinate = ClampCoordinate(guide->orientation, monitor.bounds, coordinate);
        return true;
    }

    Guide* Collection::Find(GuideId id)
    {
        const auto iterator = std::find_if(_guides.begin(), _guides.end(), [id](const Guide& guide) {
            return guide.id == id;
        });
        return iterator == _guides.end() ? nullptr : &*iterator;
    }

    const Guide* Collection::Find(GuideId id) const
    {
        const auto iterator = std::find_if(_guides.begin(), _guides.end(), [id](const Guide& guide) {
            return guide.id == id;
        });
        return iterator == _guides.end() ? nullptr : &*iterator;
    }

    const std::vector<Guide>& Collection::Guides() const
    {
        return _guides;
    }

    bool Collection::Empty() const
    {
        return _guides.empty();
    }

    std::optional<HitTestResult> Collection::HitTest(
        POINT systemPoint,
        const Monitor& monitor,
        int hitRadius) const
    {
        std::optional<HitTestResult> nearest;
        for (const auto& guide : _guides)
        {
            if (guide.monitorId != monitor.id)
            {
                continue;
            }

            const int distance = DistanceToGuide(guide, systemPoint, monitor.bounds);
            if (distance > hitRadius)
            {
                continue;
            }

            if (!nearest || distance < nearest->distance || (distance == nearest->distance && guide.id > nearest->id))
            {
                nearest = HitTestResult{ .id = guide.id, .distance = distance };
            }
        }

        return nearest;
    }

    InteractionController::InteractionController(Collection& collection) :
        _collection(collection)
    {
    }

    void InteractionController::BeginPlacement(Orientation orientation, const Monitor& monitor, int coordinate)
    {
        _interaction = Interaction{
            .kind = InteractionKind::Placement,
            .orientation = orientation,
            .monitor = monitor,
            .coordinate = ClampCoordinate(orientation, monitor.bounds, coordinate),
        };
    }

    bool InteractionController::BeginDrag(GuideId id, const Monitor& monitor)
    {
        const auto* guide = _collection.Find(id);
        if (!guide)
        {
            return false;
        }

        _interaction = Interaction{
            .kind = InteractionKind::Drag,
            .orientation = guide->orientation,
            .guideId = guide->id,
            .monitor = monitor,
            .coordinate = guide->coordinate,
        };
        return true;
    }

    void InteractionController::Update(const Monitor& monitor, int coordinate, bool removeOnCommit)
    {
        if (!Active())
        {
            return;
        }

        _interaction.monitor = monitor;
        _interaction.coordinate = ClampCoordinate(_interaction.orientation, monitor.bounds, coordinate);
        _interaction.removeOnCommit = removeOnCommit;
    }

    std::optional<GuideId> InteractionController::Commit()
    {
        if (!Active())
        {
            return std::nullopt;
        }

        std::optional<GuideId> result;
        if (_interaction.kind == InteractionKind::Placement)
        {
            if (!_interaction.removeOnCommit)
            {
                result = _collection.Add(_interaction.orientation, _interaction.monitor, _interaction.coordinate);
            }
        }
        else if (_interaction.guideId)
        {
            if (_interaction.removeOnCommit)
            {
                _collection.Remove(*_interaction.guideId);
            }
            else if (_collection.Move(*_interaction.guideId, _interaction.monitor, _interaction.coordinate))
            {
                result = _interaction.guideId;
            }
        }

        Cancel();
        return result;
    }

    void InteractionController::Cancel()
    {
        _interaction = {};
    }

    const Interaction& InteractionController::Current() const
    {
        return _interaction;
    }

    bool InteractionController::Active() const
    {
        return _interaction.kind != InteractionKind::None;
    }
}
