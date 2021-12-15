#pragma once

#include "pch.h"

#include <atomic>
#include <mmsystem.h> // sound

class Sound
{
public:
    Sound()
        : isPlaying(false)
    {}

    void Play()
    {
        if (!isPlaying)
        {
            std::thread soundThread([&]() {
                isPlaying = true;

                auto soundPlayed = PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID);
                if (!soundPlayed)
                {
                    Logger::error(L"Sound playing error");
                }

                isPlaying = false;
            });
            soundThread.detach();  
        }
    }

private:
    std::atomic<bool> isPlaying;
};