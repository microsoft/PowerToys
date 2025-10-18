#include "pch.h"
#include "OnThreadExecutor.h"

#include <utility>

OnThreadExecutor::OnThreadExecutor() :
    _worker_thread([this] { worker_thread(); })
{
}

OnThreadExecutor::~OnThreadExecutor()
{
    _shutdown_request = true;
    _task_cv.notify_one();
    if (_worker_thread.joinable())
    {
        _worker_thread.join();
    }
}

std::future<void> OnThreadExecutor::submit(task_t task)
{
    auto future = task.get_future();
    {
        std::lock_guard lock{ _task_mutex };
        _task_queue.emplace(std::move(task));
    }
    _task_cv.notify_one();
    return future;
}

void OnThreadExecutor::cancel()
{
    std::lock_guard lock{ _task_mutex };
    std::queue<task_t> emptyQueue;
    std::swap(_task_queue, emptyQueue);
    _task_cv.notify_one();
}

void OnThreadExecutor::worker_thread()
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
