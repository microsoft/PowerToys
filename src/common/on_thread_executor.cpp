#include "pch.h"

#include "on_thread_executor.h"

OnThreadExecutor::OnThreadExecutor()
  :_worker_thread{[this]() { worker_thread(); }}
{}

std::future<void> OnThreadExecutor::submit(task_t task) {
  
  auto future = task.get_future();
  std::lock_guard lock{_task_mutex};
  _task_queue.emplace(std::move(task));
  _task_cv.notify_one();
  return future;
}

void OnThreadExecutor::worker_thread() {
  while(_active)
  {
    task_t task;
    {
      std::unique_lock task_lock{_task_mutex};
      _task_cv.wait(task_lock, [this] { return !_task_queue.empty() || !_active; });
      if(!_active)
      {
        break;
      }
      task = std::move(_task_queue.front());
      _task_queue.pop();
    }
    task();
  }
}

OnThreadExecutor::~OnThreadExecutor() {
  _active = false;
  _task_cv.notify_one();
  _worker_thread.join();
}

