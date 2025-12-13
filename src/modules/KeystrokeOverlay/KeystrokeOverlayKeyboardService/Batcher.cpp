// Batcher.cpp
// Worker thread that batches and sends keystroke data through IPC.
#include "pch.h"
#include "Batcher.h"
#include <sstream>

// Escapes a string for JSON inclusion
static std::string Escape(const std::string &s)
{
    std::string o;
    o.reserve(s.size() + 4);
    for (char c : s)
    {
        switch (c)
        {
        case '"':
            o += "\\\"";
            break;
        case '\\':
            o += "\\\\";
            break;
        case '\n':
            o += "\\n";
            break;
        case '\r':
            o += "\\r";
            break;
        case '\t':
            o += "\\t";
            break;
        default:
            o += c;
        }
    }
    return o;
}

// Starts the batcher thread
void Batcher::Start()
{
    if (_run.exchange(true))
        return;
    _t = std::thread([this]
                     {
        std::vector<KeystrokeEvent> batch; batch.reserve(32);
        while (_run.load()) {
            // drain up to 32 events
            KeystrokeEvent ev;
            batch.clear();
            for (int i=0; i<32 && _q.try_pop(ev); ++i) batch.push_back(ev);

            if (!batch.empty()) {
                std::ostringstream oss;
                oss << R"({"schema":1,"events":[)";
                for (size_t i=0; i<batch.size(); ++i) {
                    const auto& e = batch[i];
                    oss << R"({"t":")" << (e.type==KeystrokeEvent::Type::Down?"down":e.type==KeystrokeEvent::Type::Up?"up":"char") << R"(",)"
                        << R"("vk":)" << e.vk << ','
                        << R"("text":")" << (e.ch ? Escape(std::string{ static_cast<char>(e.ch) }) : "") << R"(",)"
                        << R"("mods":[)" << (e.mods[0]?"\"Ctrl\"":"") 
                        << ((e.mods[0]&&e.mods[1])?",":"")
                        << (e.mods[1]?"\"Alt\"":"")
                        << (((e.mods[0]||e.mods[1])&&e.mods[2])?",":"")
                        << (e.mods[2]?"\"Shift\"":"")
                        << (((e.mods[0]||e.mods[1]||e.mods[2])&&e.mods[3])?",":"")
                        << (e.mods[3]?"\"Win\"":"")
                        << R"(],)"
                        << R"("ts":)" << (e.ts_micros/1'000'000.0)
                        << "}";
                    if (i+1<batch.size()) oss << ",";
                }
                oss << "]}";
                _pipe.EnsureClient();
                _pipe.SendFrame(oss.str());
            }
            Sleep(8); // ~120Hz batching
        } });
}

void Batcher::Stop()
{
    if (!_run.exchange(false))
        return;
    if (_t.joinable())
        _t.join();
}
