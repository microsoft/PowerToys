#pragma once

#include <future>
#include <thread>
#include <functional>
#include <queue>
#include <atomic>

class OnThreadExecutor final {
public:
  using task_t = std::packaged_task<void()>;

  OnThreadExecutor();
  ~OnThreadExecutor();
  std::future<void> submit(task_t task);

private:
  void worker_thread();

  std::thread _worker_thread;

  std::mutex _task_mutex;
  std::condition_variable _task_cv;
  std::atomic_bool _active;
  std::queue<std::packaged_task<void()>> _task_queue;
};
