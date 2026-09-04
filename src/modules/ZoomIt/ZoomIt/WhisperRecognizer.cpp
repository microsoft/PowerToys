#include "pch.h"
#include "WhisperRecognizer.h"

#include "third_party/whisper/whisper.h"

#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>
#include <ks.h>
#include <ksmedia.h>
#include <mmdeviceapi.h>

#include <array>
#include <limits>

extern TCHAR g_MicrophoneDeviceId[];
void OutputDebug( const TCHAR* format, ... );

namespace
{
    constexpr std::array<int, 3> ModelResourceIds = {
        IDR_WHISPER_MODEL_0,
        IDR_WHISPER_MODEL_1,
        IDR_WHISPER_MODEL_2,
    };

    struct ModelChunk
    {
        const std::byte* data{};
        size_t size{};
    };

    struct EmbeddedModelReader
    {
        std::array<ModelChunk, ModelResourceIds.size()> chunks{};
        size_t position{};
        size_t size{};
    };

    size_t ReadEmbeddedModel( void* context, void* output, size_t requested )
    {
        auto& reader = *static_cast<EmbeddedModelReader*>( context );
        const size_t available = reader.size - (std::min)( reader.position, reader.size );
        size_t remaining = (std::min)( requested, available );
        auto* destination = static_cast<std::byte*>( output );

        size_t chunkStart = 0;
        for( const auto& chunk : reader.chunks )
        {
            const size_t chunkEnd = chunkStart + chunk.size;
            if( remaining != 0 && reader.position < chunkEnd )
            {
                const size_t offset = reader.position > chunkStart ? reader.position - chunkStart : 0;
                const size_t count = (std::min)( remaining, chunk.size - offset );
                memcpy( destination, chunk.data + offset, count );
                destination += count;
                reader.position += count;
                remaining -= count;
            }
            chunkStart = chunkEnd;
        }

        return (std::min)( requested, available ) - remaining;
    }

    bool EmbeddedModelEof( void* context )
    {
        const auto& reader = *static_cast<EmbeddedModelReader*>( context );
        return reader.position >= reader.size;
    }

    void CloseEmbeddedModel( void* )
    {
    }

    bool LoadEmbeddedModelResources( EmbeddedModelReader& reader )
    {
        HMODULE module = GetModuleHandle( nullptr );
        for( size_t index = 0; index < ModelResourceIds.size(); ++index )
        {
            HRSRC resource = FindResource(
                module,
                MAKEINTRESOURCE( ModelResourceIds[index] ),
                RT_RCDATA );
            if( resource == nullptr )
            {
                return false;
            }

            HGLOBAL loaded = LoadResource( module, resource );
            const DWORD size = SizeofResource( module, resource );
            const void* data = loaded != nullptr ? LockResource( loaded ) : nullptr;
            if( data == nullptr || size == 0 )
            {
                return false;
            }

            reader.chunks[index] = {
                static_cast<const std::byte*>( data ),
                static_cast<size_t>( size ),
            };
            reader.size += size;
        }

        return true;
    }

    std::wstring ToWide( const std::string& text )
    {
        if( text.empty() )
        {
            return {};
        }

        const int length = MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            text.data(),
            static_cast<int>( text.size() ),
            nullptr,
            0 );
        if( length <= 0 )
        {
            return {};
        }

        std::wstring result( static_cast<size_t>( length ), L'\0' );
        MultiByteToWideChar(
            CP_UTF8,
            MB_ERR_INVALID_CHARS,
            text.data(),
            static_cast<int>( text.size() ),
            result.data(),
            length );
        return result;
    }

    std::wstring Lowercase( std::wstring value )
    {
        std::transform( value.begin(), value.end(), value.begin(), ::towlower );
        return value;
    }

    HRESULT ResolveCaptureDevice( IMMDeviceEnumerator* enumerator, IMMDevice** result )
    {
        *result = nullptr;
        if( g_MicrophoneDeviceId[0] == 0 )
        {
            return enumerator->GetDefaultAudioEndpoint( eCapture, eConsole, result );
        }

        const std::wstring selected = Lowercase( g_MicrophoneDeviceId );
        wil::com_ptr<IMMDeviceCollection> devices;
        HRESULT hr = enumerator->EnumAudioEndpoints( eCapture, DEVICE_STATE_ACTIVE, devices.put() );
        if( FAILED( hr ) )
        {
            return hr;
        }

        UINT count = 0;
        hr = devices->GetCount( &count );
        if( FAILED( hr ) )
        {
            return hr;
        }
        for( UINT index = 0; index < count; ++index )
        {
            wil::com_ptr<IMMDevice> device;
            if( FAILED( devices->Item( index, device.put() ) ) )
            {
                continue;
            }

            wil::unique_cotaskmem_string endpointId;
            if( SUCCEEDED( device->GetId( endpointId.put() ) ) &&
                endpointId &&
                selected.find( Lowercase( endpointId.get() ) ) != std::wstring::npos )
            {
                *result = device.detach();
                return S_OK;
            }
        }

        OutputDebug( L"[Dictation] Selected microphone is unavailable, using the default\n" );
        return enumerator->GetDefaultAudioEndpoint( eCapture, eConsole, result );
    }

    bool IsFloatFormat( const WAVEFORMATEX& format )
    {
        if( format.wFormatTag == WAVE_FORMAT_IEEE_FLOAT )
        {
            return true;
        }
        if( format.wFormatTag != WAVE_FORMAT_EXTENSIBLE ||
            format.cbSize < sizeof( WAVEFORMATEXTENSIBLE ) - sizeof( WAVEFORMATEX ) )
        {
            return false;
        }
        return IsEqualGUID(
            reinterpret_cast<const WAVEFORMATEXTENSIBLE&>( format ).SubFormat,
            KSDATAFORMAT_SUBTYPE_IEEE_FLOAT );
    }

    bool IsPcmFormat( const WAVEFORMATEX& format )
    {
        if( format.wFormatTag == WAVE_FORMAT_PCM )
        {
            return true;
        }
        if( format.wFormatTag != WAVE_FORMAT_EXTENSIBLE ||
            format.cbSize < sizeof( WAVEFORMATEXTENSIBLE ) - sizeof( WAVEFORMATEX ) )
        {
            return false;
        }
        return IsEqualGUID(
            reinterpret_cast<const WAVEFORMATEXTENSIBLE&>( format ).SubFormat,
            KSDATAFORMAT_SUBTYPE_PCM );
    }

    float ReadSample( const BYTE* sample, const WAVEFORMATEX& format )
    {
        if( IsFloatFormat( format ) && format.wBitsPerSample == 32 )
        {
            return *reinterpret_cast<const float*>( sample );
        }

        if( !IsPcmFormat( format ) )
        {
            return 0.0f;
        }

        switch( format.wBitsPerSample )
        {
        case 16:
            return static_cast<float>( *reinterpret_cast<const int16_t*>( sample ) ) / 32768.0f;
        case 24:
        {
            int32_t value =
                static_cast<int32_t>( sample[0] ) |
                ( static_cast<int32_t>( sample[1] ) << 8 ) |
                ( static_cast<int32_t>( sample[2] ) << 16 );
            if( value & 0x00800000 )
            {
                value |= static_cast<int32_t>( 0xFF000000 );
            }
            return static_cast<float>( value ) / 8388608.0f;
        }
        case 32:
            return static_cast<float>( *reinterpret_cast<const int32_t*>( sample ) ) / 2147483648.0f;
        default:
            return 0.0f;
        }
    }

    std::vector<float> ResampleToWhisper( const std::vector<float>& input, uint32_t sampleRate )
    {
        if( input.empty() || sampleRate == 0 )
        {
            return {};
        }
        if( sampleRate == WHISPER_SAMPLE_RATE )
        {
            return input;
        }

        const size_t outputSize = static_cast<size_t>(
            static_cast<double>( input.size() ) * WHISPER_SAMPLE_RATE / sampleRate );
        std::vector<float> output( outputSize );
        const double step = static_cast<double>( sampleRate ) / WHISPER_SAMPLE_RATE;
        for( size_t index = 0; index < outputSize; ++index )
        {
            const double source = index * step;
            const size_t left = (std::min)( static_cast<size_t>( source ), input.size() - 1 );
            const size_t right = (std::min)( left + 1, input.size() - 1 );
            const float fraction = static_cast<float>( source - left );
            output[index] = input[left] + ( input[right] - input[left] ) * fraction;
        }
        return output;
    }
}

struct WhisperRecognizer::Impl
{
    Impl()
    {
        audioEvent.create();
        stopEvent.create( wil::EventOptions::ManualReset );
    }

    whisper_context* context{};
    wil::com_ptr<IMMDeviceEnumerator> deviceEnumerator;
    wil::com_ptr<IMMDevice> device;
    wil::com_ptr<IAudioClient> audioClient;
    wil::com_ptr<IAudioCaptureClient> captureClient;
    wil::unique_cotaskmem_ptr<WAVEFORMATEX> format;
    wil::unique_event audioEvent;
    wil::unique_event stopEvent;
    std::thread captureThread;
    wil::com_ptr<IStream> abandonedCaptureStream;
    std::vector<float> samples;
    std::atomic<HRESULT> captureFailure{ S_OK };
    Failure failure{ Failure::None };

    bool InitializeAudio()
    {
        deviceEnumerator.reset();
        device.reset();
        audioClient.reset();
        captureClient.reset();
        format.reset();

        HRESULT hr = CoCreateInstance(
            __uuidof( MMDeviceEnumerator ),
            nullptr,
            CLSCTX_ALL,
            IID_PPV_ARGS( deviceEnumerator.put() ) );
        if( FAILED( hr ) )
        {
            failure = Failure::NotSupported;
            return false;
        }

        hr = ResolveCaptureDevice( deviceEnumerator.get(), device.put() );
        if( FAILED( hr ) )
        {
            failure = hr == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::NotSupported;
            return false;
        }

        hr = device->Activate(
            __uuidof( IAudioClient ),
            CLSCTX_ALL,
            nullptr,
            audioClient.put_void() );
        if( FAILED( hr ) )
        {
            failure = hr == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::Unknown;
            return false;
        }

        WAVEFORMATEX* rawFormat = nullptr;
        hr = audioClient->GetMixFormat( &rawFormat );
        if( FAILED( hr ) )
        {
            failure = Failure::Unknown;
            return false;
        }
        format.reset( rawFormat );

        if( format->nChannels == 0 || format->nSamplesPerSec == 0 ||
            ( !IsFloatFormat( *format ) && !IsPcmFormat( *format ) ) )
        {
            failure = Failure::NotSupported;
            return false;
        }

        hr = audioClient->Initialize(
            AUDCLNT_SHAREMODE_SHARED,
            AUDCLNT_STREAMFLAGS_EVENTCALLBACK | AUDCLNT_STREAMFLAGS_NOPERSIST,
            0,
            0,
            format.get(),
            nullptr );
        if( FAILED( hr ) )
        {
            failure = hr == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::Unknown;
            return false;
        }

        hr = audioClient->SetEventHandle( audioEvent.get() );
        if( FAILED( hr ) )
        {
            failure = Failure::Unknown;
            return false;
        }
        hr = audioClient->GetService(
            __uuidof( IAudioCaptureClient ),
            captureClient.put_void() );
        if( FAILED( hr ) )
        {
            failure = Failure::Unknown;
            return false;
        }

        return true;
    }

    bool DrainAudio( IAudioCaptureClient* threadCaptureClient )
    {
        for( ;; )
        {
            UINT32 frames = 0;
            const HRESULT packetResult = threadCaptureClient->GetNextPacketSize( &frames );
            if( FAILED( packetResult ) )
            {
                captureFailure = packetResult;
                return false;
            }
            if( frames == 0 )
            {
                return true;
            }

            BYTE* data = nullptr;
            DWORD flags = 0;
            const HRESULT bufferResult =
                threadCaptureClient->GetBuffer( &data, &frames, &flags, nullptr, nullptr );
            if( FAILED( bufferResult ) )
            {
                captureFailure = bufferResult;
                return false;
            }

            const size_t oldSize = samples.size();
            samples.resize( oldSize + frames );
            if( flags & AUDCLNT_BUFFERFLAGS_SILENT )
            {
                std::fill( samples.begin() + oldSize, samples.end(), 0.0f );
            }
            else
            {
                const size_t bytesPerSample = format->wBitsPerSample / 8;
                for( UINT32 frame = 0; frame < frames; ++frame )
                {
                    float mono = 0.0f;
                    const BYTE* frameData = data + static_cast<size_t>( frame ) * format->nBlockAlign;
                    for( WORD channel = 0; channel < format->nChannels; ++channel )
                    {
                        mono += ReadSample( frameData + channel * bytesPerSample, *format );
                    }
                    samples[oldSize + frame] = mono / format->nChannels;
                }
            }

            const HRESULT releaseResult = threadCaptureClient->ReleaseBuffer( frames );
            if( FAILED( releaseResult ) )
            {
                captureFailure = releaseResult;
                return false;
            }
        }
    }

    void CaptureAudio( IStream* marshaledCaptureClient )
    {
        const HRESULT initializeResult = CoInitializeEx( nullptr, COINIT_MULTITHREADED );
        if( FAILED( initializeResult ) )
        {
            // The dictation worker is already COM-initialized and must release
            // the marshal packet if this thread cannot enter an apartment.
            abandonedCaptureStream.attach( marshaledCaptureClient );
            captureFailure = initializeResult;
            return;
        }

        wil::com_ptr<IAudioCaptureClient> threadCaptureClient;
        const HRESULT unmarshalResult = CoGetInterfaceAndReleaseStream(
            marshaledCaptureClient,
            __uuidof( IAudioCaptureClient ),
            threadCaptureClient.put_void() );
        if( FAILED( unmarshalResult ) )
        {
            captureFailure = unmarshalResult;
            CoUninitialize();
            return;
        }

        HANDLE waits[] = { stopEvent.get(), audioEvent.get() };
        for( ;; )
        {
            const DWORD wait = WaitForMultipleObjects( ARRAYSIZE( waits ), waits, FALSE, INFINITE );
            if( wait == WAIT_OBJECT_0 )
            {
                DrainAudio( threadCaptureClient.get() );
                break;
            }
            if( wait != WAIT_OBJECT_0 + 1 )
            {
                captureFailure = HRESULT_FROM_WIN32( GetLastError() );
                break;
            }
            if( !DrainAudio( threadCaptureClient.get() ) )
            {
                break;
            }
        }

        threadCaptureClient.reset();
        CoUninitialize();
    }

    void ReleaseAudio()
    {
        if( audioClient )
        {
            audioClient->Stop();
        }
        if( stopEvent )
        {
            stopEvent.SetEvent();
        }
        if( captureThread.joinable() )
        {
            captureThread.join();
        }
        if( abandonedCaptureStream )
        {
            CoReleaseMarshalData( abandonedCaptureStream.get() );
            abandonedCaptureStream.reset();
        }
        captureClient.reset();
        audioClient.reset();
        device.reset();
        deviceEnumerator.reset();
        format.reset();
    }
};

WhisperRecognizer::WhisperRecognizer() :
    m_impl( std::make_unique<Impl>() )
{
}

WhisperRecognizer::~WhisperRecognizer()
{
    Shutdown();
}

bool WhisperRecognizer::Prepare()
{
#if defined(_M_X64)
    if( !IsProcessorFeaturePresent( PF_AVX2_INSTRUCTIONS_AVAILABLE ) )
    {
        OutputDebug( L"[Dictation] Whisper requires AVX2 on x64; using SAPI\n" );
        m_impl->failure = Failure::NotSupported;
        return false;
    }
#endif

    if( m_impl->context != nullptr )
    {
        return true;
    }

    EmbeddedModelReader reader;
    if( !LoadEmbeddedModelResources( reader ) )
    {
        OutputDebug( L"[Dictation] Embedded Whisper model is missing\n" );
        m_impl->failure = Failure::NotSupported;
        return false;
    }

    whisper_model_loader loader = {
        &reader,
        ReadEmbeddedModel,
        EmbeddedModelEof,
        CloseEmbeddedModel,
    };
    auto parameters = whisper_context_default_params();
    parameters.use_gpu = false;

    whisper_log_set( []( ggml_log_level, const char*, void* ) {}, nullptr );
    m_impl->context = whisper_init_with_params( &loader, parameters );
    if( m_impl->context == nullptr )
    {
        OutputDebug( L"[Dictation] Embedded Whisper model could not be loaded\n" );
        m_impl->failure = Failure::NotSupported;
        return false;
    }

    m_impl->failure = Failure::None;
    OutputDebug( L"[Dictation] Embedded Whisper model loaded\n" );
    return true;
}

bool WhisperRecognizer::Start()
{
    if( m_impl->context == nullptr || !m_impl->InitializeAudio() )
    {
        return false;
    }

    m_impl->samples.clear();
    m_impl->captureFailure = S_OK;
    m_impl->audioEvent.ResetEvent();
    m_impl->stopEvent.ResetEvent();

    wil::com_ptr<IStream> marshaledCaptureClient;
    const HRESULT marshalResult = CoMarshalInterThreadInterfaceInStream(
        __uuidof( IAudioCaptureClient ),
        m_impl->captureClient.get(),
        marshaledCaptureClient.put() );
    if( FAILED( marshalResult ) )
    {
        m_impl->failure = Failure::Unknown;
        m_impl->ReleaseAudio();
        return false;
    }

    m_impl->captureClient.reset();
    m_impl->captureThread = std::thread( [this, stream = marshaledCaptureClient.detach()] {
        m_impl->CaptureAudio( stream );
    } );

    const HRESULT hr = m_impl->audioClient->Start();
    if( FAILED( hr ) )
    {
        m_impl->failure = hr == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::Unknown;
        m_impl->ReleaseAudio();
        return false;
    }

    m_impl->failure = Failure::None;
    return true;
}

std::wstring WhisperRecognizer::Stop(
    bool discard,
    std::atomic_bool& cancelRequested )
{
    const uint32_t sampleRate = m_impl->format ? m_impl->format->nSamplesPerSec : 0;
    m_impl->ReleaseAudio();

    const HRESULT captureFailure = m_impl->captureFailure;
    if( FAILED( captureFailure ) )
    {
        OutputDebug( L"[Dictation] Whisper audio capture failed hr=0x%08x\n", captureFailure );
        m_impl->failure =
            captureFailure == E_ACCESSDENIED ? Failure::MicrophoneDenied : Failure::Unknown;
        m_impl->samples.clear();
        return {};
    }

    if( discard || cancelRequested || m_impl->samples.empty() )
    {
        m_impl->samples.clear();
        return {};
    }

    std::vector<float> samples = ResampleToWhisper( m_impl->samples, sampleRate );
    m_impl->samples.clear();
    if( samples.empty() || samples.size() > static_cast<size_t>( (std::numeric_limits<int>::max)() ) )
    {
        return {};
    }

    auto parameters = whisper_full_default_params( WHISPER_SAMPLING_GREEDY );
    const unsigned int processors = (std::max)( 1u, std::thread::hardware_concurrency() );
    parameters.n_threads = static_cast<int>( (std::min)( 8u, processors ) );
    parameters.language = "en";
    parameters.translate = false;
    parameters.no_context = true;
    parameters.no_timestamps = true;
    parameters.print_special = false;
    parameters.print_progress = false;
    parameters.print_realtime = false;
    parameters.print_timestamps = false;
    parameters.suppress_blank = true;
    parameters.suppress_non_speech_tokens = true;
    parameters.abort_callback = []( void* context ) {
        return static_cast<std::atomic_bool*>( context )->load();
    };
    parameters.abort_callback_user_data = &cancelRequested;

    const int result = whisper_full(
        m_impl->context,
        parameters,
        samples.data(),
        static_cast<int>( samples.size() ) );
    if( result != 0 || cancelRequested )
    {
        if( result != 0 && !cancelRequested )
        {
            m_impl->failure = Failure::Unknown;
        }
        return {};
    }

    std::string utf8;
    const int segments = whisper_full_n_segments( m_impl->context );
    for( int index = 0; index < segments; ++index )
    {
        if( const char* text = whisper_full_get_segment_text( m_impl->context, index ) )
        {
            utf8 += text;
        }
    }

    return cancelRequested ? std::wstring{} : ToWide( utf8 );
}

void WhisperRecognizer::Shutdown()
{
    m_impl->ReleaseAudio();
    if( m_impl->context != nullptr )
    {
        whisper_free( m_impl->context );
        m_impl->context = nullptr;
    }
}

WhisperRecognizer::Failure WhisperRecognizer::GetFailure() const
{
    return m_impl->failure;
}
