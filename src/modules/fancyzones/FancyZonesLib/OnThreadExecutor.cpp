#include "pch.h"

#include <common/logger/call_tracer.h>

#include "on_thread_executor.h"

OnThreadExecutor::OnThreadExecutor() :
    _shutdown_request{ false }, _worker_thread{ [this] { worker_thread(); } }
{
}

std::future<void> OnThreadExecutor::submit(task_t task)
{
    auto future = task.get_future();
    std::lock_guard lock{ _task_mutex };
    _task_queue.emplace(std::move(task));
    _task_cv.notify_one();
    return future;
}

void OnThreadExecutor::cancel()
{
    std::lock_guard lock{ _task_mutex };
    _task_queue = {};
    _task_cv.notify_one();
}


void OnThreadExecutor::worker_thread()
{
    while (!_shutdown_request)
    {
        task_t task;
        {
            CallTracer callTracer(__FUNCTION__ "(loop)");
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

OnThreadExecutor::~OnThreadExecutor()
{
    _shutdown_request = true;
    _task_cv.notify_one();
    _worker_thread.join();
}
