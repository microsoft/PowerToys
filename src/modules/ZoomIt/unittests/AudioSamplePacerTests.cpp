#include <CppUnitTest.h>

#include "AudioSamplePacer.h"
#include "AudioQueueLimits.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace AudioQueueLimitTests
{
    TEST_CLASS(DurationTests)
    {
    public:
        TEST_METHOD(EncodedQueueHoldsTwentySecondsAndLiveBufferOneSecondAt48kStereo)
        {
            constexpr uint32_t sampleRate = 48000;
            constexpr uint32_t channels = 2;
            constexpr size_t samplesPerSecond = static_cast<size_t>(sampleRate) * channels;

            Assert::AreEqual(samplesPerSecond * 20 * sizeof(float), audio_queue::MaxEncodedFloatBytes(sampleRate, channels));
            Assert::AreEqual(samplesPerSecond, audio_queue::MaxLiveSampleCount(sampleRate, channels));
        }

        TEST_METHOD(LimitsFollowDeviceRateAndChannelCount)
        {
            constexpr uint32_t sampleRate = 44100;
            constexpr uint32_t channels = 6;
            constexpr size_t samplesPerSecond = static_cast<size_t>(sampleRate) * channels;

            Assert::AreEqual(samplesPerSecond * 20 * sizeof(float), audio_queue::MaxEncodedFloatBytes(sampleRate, channels));
            Assert::AreEqual(samplesPerSecond, audio_queue::MaxLiveSampleCount(sampleRate, channels));
        }
    };
}

namespace AudioSamplePacerTests
{
    TEST_CLASS(ClockTests)
    {
    public:
        TEST_METHOD(EmitsOnlyAtRealTime)
        {
            AudioSamplePacer pacer(48000);
            uint64_t frame = 0;
            Assert::IsFalse(pacer.NextChunk(99'999, frame));
            Assert::IsTrue(pacer.NextChunk(100'000, frame));
            Assert::AreEqual(static_cast<uint64_t>(0), frame);
            Assert::IsFalse(pacer.NextChunk(100'000, frame));
            Assert::IsTrue(pacer.NextChunk(200'000, frame));
            Assert::AreEqual(static_cast<uint64_t>(480), frame);
        }

        TEST_METHOD(OneSecondCannotProduceMoreThanOneSecondOfAudio)
        {
            AudioSamplePacer pacer(48000);
            uint64_t frame = 0;
            uint32_t chunks = 0;
            for (int64_t elapsedTicks = 0; elapsedTicks <= 10'000'000; elapsedTicks += 1000)
            {
                if (pacer.NextChunk(elapsedTicks, frame))
                {
                    ++chunks;
                }
            }
            Assert::AreEqual(100u, chunks);
        }

        TEST_METHOD(SkipsExcessBacklogAfterLongPause)
        {
            AudioSamplePacer pacer(48000);
            uint64_t frame = 0;
            Assert::IsTrue(pacer.NextChunk(100'000, frame));
            Assert::IsTrue(pacer.NextChunk(50'000'000, frame));
            Assert::IsTrue(frame >= static_cast<uint64_t>(4 * 48000));
            Assert::IsTrue(frame < static_cast<uint64_t>(5 * 48000));
        }

        TEST_METHOD(UsesDeviceSampleRate)
        {
            AudioSamplePacer pacer(44100);
            uint64_t frame = 0;
            Assert::AreEqual(441u, pacer.FramesPerChunk());
            Assert::IsTrue(pacer.NextChunk(100'000, frame));
            Assert::IsTrue(pacer.NextChunk(200'000, frame));
            Assert::AreEqual(static_cast<uint64_t>(441), frame);
        }
    };
}
