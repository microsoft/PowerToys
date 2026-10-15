#pragma once

#include <atomic>
#include <memory>
#include <string>

class WhisperRecognizer
{
public:
    enum class Failure
    {
        None,
        NotSupported,
        MicrophoneDenied,
        Unknown,
    };

    WhisperRecognizer();
    ~WhisperRecognizer();

    WhisperRecognizer( const WhisperRecognizer& ) = delete;
    WhisperRecognizer& operator=( const WhisperRecognizer& ) = delete;

    bool Prepare();
    bool Start();
    std::wstring Stop( bool discard, std::atomic_bool& cancelRequested );
    void Shutdown();

    Failure GetFailure() const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};
