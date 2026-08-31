#include "pch.h"
#include "LaserStroke.h"

#include <algorithm>
#include <cmath>

namespace LaserPointerCore
{
    namespace
    {
        // Cubic ease-out, matching the easing excalidraw applies to both decay terms.
        constexpr float EaseOut(float t) noexcept
        {
            const float inv = 1.0f - t;
            return 1.0f - inv * inv * inv;
        }

        constexpr float Clamp01(float v) noexcept
        {
            return v < 0.0f ? 0.0f : (v > 1.0f ? 1.0f : v);
        }

        // Number of segments used to approximate each end cap semicircle. Eight is
        // enough to look round at the widths a laser pointer uses without making the
        // per-frame vertex count grow noticeably.
        constexpr int CAP_SEGMENTS = 8;

        constexpr float PI = 3.14159265358979323846f;
    }

    void LaserStroke::SetOptions(const StrokeOptions& options)
    {
        m_options = options;
        m_options.streamline = std::clamp(m_options.streamline, 0.0f, 0.95f);
        m_options.size = (std::max)(m_options.size, 1.0f);
        m_options.decayTimeMs = (std::max)(m_options.decayTimeMs, 1u);
        m_options.decayLength = (std::max)(m_options.decayLength, 1u);
        m_options.minDistance = (std::max)(m_options.minDistance, 0.0f);
    }

    void LaserStroke::AddPoint(float x, float y, uint64_t timestampMs)
    {
        if (m_finished)
        {
            return;
        }

        if (m_points.empty())
        {
            m_points.push_back({ x, y, timestampMs });
            return;
        }

        const Sample& last = m_points.back();

        // Streamline: pull the incoming sample back towards the previous one. This is
        // the smoothing excalidraw applies on every added point.
        const float t = 1.0f - m_options.streamline;
        const float sx = last.x + (x - last.x) * t;
        const float sy = last.y + (y - last.y) * t;

        const float dx = sx - last.x;
        const float dy = sy - last.y;
        if ((dx * dx + dy * dy) < (m_options.minDistance * m_options.minDistance))
        {
            return;
        }

        m_points.push_back({ sx, sy, timestampMs });

        // A sample can never outlive decayTimeMs, so the deque is naturally bounded;
        // this only guards against a pathological render stall.
        constexpr size_t MAX_SAMPLES = 4096;
        while (m_points.size() > MAX_SAMPLES)
        {
            m_points.pop_front();
        }
    }

    bool LaserStroke::Prune(uint64_t nowMs)
    {
        while (!m_points.empty())
        {
            const uint64_t birth = m_points.front().t;
            const uint64_t age = nowMs > birth ? nowMs - birth : 0;
            if (age < m_options.decayTimeMs)
            {
                break;
            }
            m_points.pop_front();
        }

        return !m_points.empty();
    }

    void LaserStroke::Clear() noexcept
    {
        m_points.clear();
        m_finished = false;
    }

    float LaserStroke::HalfWidthAt(size_t index, uint64_t nowMs) const
    {
        if (index >= m_points.size())
        {
            return 0.0f;
        }

        const size_t count = m_points.size();
        const Sample& sample = m_points[index];

        const uint64_t age = nowMs > sample.t ? nowMs - sample.t : 0;
        const float timeTerm = Clamp01(1.0f - static_cast<float>(age) / static_cast<float>(m_options.decayTimeMs));

        const float fromEnd = static_cast<float>((count - 1) - index);
        const float decayLength = static_cast<float>(m_options.decayLength);
        const float lengthTerm = Clamp01((decayLength - (std::min)(decayLength, fromEnd)) / decayLength);

        return m_options.size * 0.5f * (std::min)(EaseOut(lengthTerm), EaseOut(timeTerm));
    }

    size_t LaserStroke::BuildOutline(uint64_t nowMs, std::vector<Vec2>& outline, float widthScale) const
    {
        outline.clear();

        const size_t count = m_points.size();
        if (count == 0)
        {
            return 0;
        }

        if (count == 1)
        {
            // A stationary pointer still shows the head dot.
            const float radius = HalfWidthAt(0, nowMs) * widthScale;
            if (radius <= 0.05f)
            {
                return 0;
            }

            const Sample& p = m_points[0];
            const int segments = CAP_SEGMENTS * 2;
            outline.reserve(static_cast<size_t>(segments));
            for (int i = 0; i < segments; ++i)
            {
                const float angle = (2.0f * PI * static_cast<float>(i)) / static_cast<float>(segments);
                outline.push_back({ p.x + std::cos(angle) * radius, p.y + std::sin(angle) * radius });
            }
            return outline.size();
        }

        // Per-sample half widths and unit normals. The direction at an interior sample
        // is the central difference of its neighbours, which keeps the offset curves
        // smooth through corners; the ends fall back to the adjacent segment.
        m_radii.assign(count, 0.0f);
        m_normals.assign(count, Vec2{ 0.0f, 0.0f });
        std::vector<float>& radii = m_radii;
        std::vector<Vec2>& normals = m_normals;

        Vec2 lastNormal{ 0.0f, 0.0f };
        bool hasNormal = false;

        for (size_t i = 0; i < count; ++i)
        {
            radii[i] = HalfWidthAt(i, nowMs) * widthScale;

            const Sample& prev = m_points[i == 0 ? 0 : i - 1];
            const Sample& next = m_points[i + 1 >= count ? count - 1 : i + 1];

            float dx = next.x - prev.x;
            float dy = next.y - prev.y;
            const float length = std::sqrt(dx * dx + dy * dy);

            if (length < 1e-4f)
            {
                // Coincident neighbours: reuse the previous normal rather than emitting
                // a degenerate one that would pinch the outline.
                normals[i] = hasNormal ? lastNormal : Vec2{ 0.0f, 1.0f };
                continue;
            }

            dx /= length;
            dy /= length;
            lastNormal = { -dy, dx };
            hasNormal = true;
            normals[i] = lastNormal;
        }

        float maxRadius = 0.0f;
        for (const float r : radii)
        {
            maxRadius = (std::max)(maxRadius, r);
        }

        if (maxRadius <= 0.05f)
        {
            return 0;
        }

        outline.reserve(count * 2 + CAP_SEGMENTS * 2 + 2);

        // Left side, tail to head.
        for (size_t i = 0; i < count; ++i)
        {
            outline.push_back({ m_points[i].x + normals[i].x * radii[i],
                                m_points[i].y + normals[i].y * radii[i] });
        }

        // Round cap around the head.
        const Sample& head = m_points[count - 1];
        const Vec2& headNormal = normals[count - 1];
        const float headRadius = radii[count - 1];
        if (headRadius > 0.05f)
        {
            const float startAngle = std::atan2(headNormal.y, headNormal.x);
            for (int i = 1; i < CAP_SEGMENTS; ++i)
            {
                const float angle = startAngle - (PI * static_cast<float>(i)) / static_cast<float>(CAP_SEGMENTS);
                outline.push_back({ head.x + std::cos(angle) * headRadius,
                                    head.y + std::sin(angle) * headRadius });
            }
        }

        // Right side, head back to tail.
        for (size_t i = count; i-- > 0;)
        {
            outline.push_back({ m_points[i].x - normals[i].x * radii[i],
                                m_points[i].y - normals[i].y * radii[i] });
        }

        // Round cap around the tail. Usually collapsed to a point by the taper, but a
        // freshly started stroke has a real width here.
        const Sample& tail = m_points[0];
        const Vec2& tailNormal = normals[0];
        const float tailRadius = radii[0];
        if (tailRadius > 0.05f)
        {
            const float startAngle = std::atan2(-tailNormal.y, -tailNormal.x);
            for (int i = 1; i < CAP_SEGMENTS; ++i)
            {
                const float angle = startAngle - (PI * static_cast<float>(i)) / static_cast<float>(CAP_SEGMENTS);
                outline.push_back({ tail.x + std::cos(angle) * tailRadius,
                                    tail.y + std::sin(angle) * tailRadius });
            }
        }

        return outline.size();
    }
}
