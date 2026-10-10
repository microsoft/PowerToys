#pragma once

#include <algorithm>
#include <cstdint>

class AudioSamplePacer
{
public:
    explicit AudioSamplePacer(uint32_t sampleRate) :
        m_sampleRate(sampleRate),
        m_framesPerChunk((std::max)(1u, sampleRate / 100))
    {
    }

    bool NextChunk(int64_t elapsedTicks, uint64_t& firstFrame)
    {
        if (elapsedTicks < 0)
        {
            return false;
        }

        const auto dueFrames = static_cast<uint64_t>(elapsedTicks) * m_sampleRate / 10'000'000;
        if (dueFrames < m_nextFrame + m_framesPerChunk)
        {
            return false;
        }

        if (dueFrames - m_nextFrame > m_sampleRate)
        {
            m_nextFrame = ((dueFrames - m_sampleRate) / m_framesPerChunk) * m_framesPerChunk;
        }

        firstFrame = m_nextFrame;
        m_nextFrame += m_framesPerChunk;
        return true;
    }

    uint32_t FramesPerChunk() const { return m_framesPerChunk; }

private:
    uint32_t m_sampleRate;
    uint32_t m_framesPerChunk;
    uint64_t m_nextFrame = 0;
};
