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