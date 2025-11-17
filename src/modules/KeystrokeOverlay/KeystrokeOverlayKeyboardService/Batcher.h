// Batcher.h
// Worker thread that batches and sends keystroke data through Named Pipe server.
#pragma once
#include "EventQueue.h"
#include "KeystrokeEvent.h"
#include "PipeServer.h"
#include <thread>
#include <atomic>

class Batcher
{
public:
    Batcher(SpscRing<KeystrokeEvent, 1024> &q) : _q(q) {}
    void Start();
    void Stop();

private:
    SpscRing<KeystrokeEvent, 1024> &_q;
    PipeServer _pipe;
    std::atomic<bool> _run{false};
    std::thread _t;
};
