#pragma once

#include <future>
#include <thread>
#include <functional>
#include <queue>
#include <atomic>

// OnThreadExecutor allows its caller to off-load some work to a persistently running background thread.
// This might come in handy if you use the API which sets thread-wide global state and the state needs
// to be isolated.

class OnThreadExecutor final
{
public:
    using task_t = std::packaged_task<void()>;

    OnThreadExecutor();
    ~OnThreadExecutor();
    std::future<void> submit(task_t task);
    void cancel();

private:
    void worker_thread();

    std::mutex _task_mutex;
    std::condition_variable _task_cv;
    std::atomic_bool _shutdown_request;
    std::queue<std::packaged_task<void()>> _task_queue;
    std::thread _worker_thread;
};
