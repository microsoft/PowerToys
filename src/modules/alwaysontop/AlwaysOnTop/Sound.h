#pragma once

#include "pch.h"

#include <atomic>
#include <mmsystem.h> // sound

class Sound
{
public:
    enum class Type
    {
        On,
        Off,
        IncreaseOpacity,
        DecreaseOpacity,
    };
    
    Sound()
        : isPlaying(false)
    {}

    void Play(Type type)
    {
        BOOL success = false;
        switch (type)
        {
        case Type::On:
            success = PlaySound(TEXT("Media\\Speech On.wav"), NULL, SND_FILENAME | SND_ASYNC);
            break;
        case Type::Off:
            success = PlaySound(TEXT("Media\\Speech Sleep.wav"), NULL, SND_FILENAME | SND_ASYNC);
            break;
        case Type::IncreaseOpacity:
            // Use a higher frequency beep for increase (more opaque)
            Beep(800, 80);
            success = TRUE;
            break;
        case Type::DecreaseOpacity:
            // Use a lower frequency beep for decrease (more transparent)
            Beep(400, 80);
            success = TRUE;
            break;
        default:
            break;
        }

        if (!success)
        {
            Logger::error(L"Sound playing error");
        }
    }

private:
    std::atomic<bool> isPlaying;
};