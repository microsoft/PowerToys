#pragma once

#include <cstddef>
#include <cstdint>
#include <deque>
#include <vector>

// Excalidraw-equivalent laser trail model, kept free of Windows dependencies so it
// can be reasoned about (and unit tested) on its own.
//
// The model mirrors @excalidraw/laser-pointer as it is consumed by excalidraw's
// laser trails:
//   * incoming points are smoothed towards the previous point by `streamline`
//     (lerp(previous, raw, 1 - streamline)),
//   * every sample carries its own timestamp,
//   * the half width at a sample is
//         size/2 * min(easeOut(lengthTerm), easeOut(timeTerm))
//     where lengthTerm tapers the trail over the trailing `decayLength` samples and
//     timeTerm shrinks each sample away over `decayTimeMs`.
// The result is a comet-shaped stroke that is widest at the head and burns away
// from the tail, which is what the excalidraw laser pointer looks like.
namespace LaserPointerCore
{
    struct Vec2
    {
        float x = 0.0f;
        float y = 0.0f;
    };

    struct StrokeOptions
    {
        // Base stroke width (full width at the head), in device independent pixels.
        float size = 10.0f;
        // Smoothing factor in [0, 0.95]. Higher means smoother but laggier.
        float streamline = 0.4f;
        // How long an individual sample takes to shrink to nothing.
        uint32_t decayTimeMs = 1000;
        // How many trailing samples the taper is spread over.
        uint32_t decayLength = 50;
        // Samples closer than this to the previous one are dropped. Excalidraw only
        // rejects exact duplicates because the browser feeds it one point per frame;
        // we sample on our own render tick but still guard against a stationary
        // pointer piling up coincident samples.
        float minDistance = 0.75f;
    };

    class LaserStroke
    {
    public:
        void SetOptions(const StrokeOptions& options);
        const StrokeOptions& Options() const noexcept { return m_options; }

        // Feeds a raw pointer sample. `timestampMs` is the sample's own birth time and
        // drives its decay, exactly like the timestamp excalidraw smuggles through the
        // pressure channel.
        void AddPoint(float x, float y, uint64_t timestampMs);

        // Drops samples that have fully decayed. Returns true while the stroke still
        // has something left to draw.
        bool Prune(uint64_t nowMs);

        void Clear() noexcept;

        // Marks the stroke as no longer accepting points; it keeps decaying until empty.
        void Finish() noexcept { m_finished = true; }
        bool Finished() const noexcept { return m_finished; }
        bool Empty() const noexcept { return m_points.empty(); }
        size_t PointCount() const noexcept { return m_points.size(); }

        // Builds the closed outline polygon of the trail at `nowMs` into `outline`
        // (cleared first). `widthScale` scales every half width, which is how the
        // renderer gets the wider glow pass and the narrower bright core pass out of
        // the same centerline. Returns the number of vertices produced; 0 means there
        // is nothing visible to draw this frame.
        size_t BuildOutline(uint64_t nowMs, std::vector<Vec2>& outline, float widthScale = 1.0f) const;

        // Half width of the sample at `index`, in device independent pixels.
        float HalfWidthAt(size_t index, uint64_t nowMs) const;

    private:
        struct Sample
        {
            float x = 0.0f;
            float y = 0.0f;
            uint64_t t = 0;
        };

        StrokeOptions m_options{};
        std::deque<Sample> m_points;
        bool m_finished = false;

        // Scratch buffers reused by BuildOutline so the render loop does not allocate
        // on every frame (it is called once per fill pass, three times per frame).
        mutable std::vector<float> m_radii;
        mutable std::vector<Vec2> m_normals;
    };
}
