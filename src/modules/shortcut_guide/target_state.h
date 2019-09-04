#pragma once
#include <deque>
#include <thread>
#include <mutex>
#include <condition_variable>
#include <chrono>
#include "shortcut_guide.h"

struct KeyEvent {
  bool key_down;
  unsigned vk_code;
};

class TargetState {
public:
  TargetState(int ms_delay);
  bool signal_event(unsigned vk_code, bool key_down);
  void was_hiden();
  void exit();
  void set_delay(int ms_delay);
private:
  KeyEvent next();
  void handle_hidden();
  void handle_timeout();
  void handle_shown();
  void thread_proc();
  std::mutex mutex;
  std::condition_variable cv;
  std::chrono::system_clock::time_point winkey_timestamp, singnal_timestamp;
  std::chrono::milliseconds delay;
  std::deque<KeyEvent> events;
  enum { Hidden, Timeout, Shown, Exiting } state = Hidden;
  bool key_was_pressed = false;
  std::thread thread;
};
