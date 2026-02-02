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

    OnThreadExecutor() :
        _shutdown_request{ false },
        _worker_thread{ [this] { worker_thread(); } }
    {
    }

    ~OnThreadExecutor()
    {
        _shutdown_request = true;
        _task_cv.notify_one();
        _worker_thread.join();
    }

    std::future<void> submit(task_t task)
    {
        auto future = task.get_future();
        std::lock_guard lock{ _task_mutex };
        _task_queue.emplace(std::move(task));
        _task_cv.notify_one();
        return future;
    }

    void cancel()
    {
        std::lock_guard lock{ _task_mutex };
        _task_queue = {};
        _task_cv.notify_one();
    }

private:
    void worker_thread()
    {
        while (!_shutdown_request)
        {
            task_t task;
            {
                std::unique_lock task_lock{ _task_mutex };
                _task_cv.wait(task_lock, [this] { return !_task_queue.empty() || _shutdown_request; });
                if (_shutdown_request)
                {
                    return;
                }
                task = std::move(_task_queue.front());
                _task_queue.pop();
            }
            task();
        }
    }

    std::mutex _task_mutex;
    std::condition_variable _task_cv;
    std::atomic_bool _shutdown_request;
    std::queue<std::packaged_task<void()>> _task_queue;
    std::thread _worker_thread;
};
