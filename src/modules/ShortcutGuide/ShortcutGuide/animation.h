#pragma once
#include <chrono>

/*
  Usage:
    When creating animation constructor takes one parameter - how long
    should the animation take in seconds.

    Call reset() when starting animation.

    When rendering, call value() to get value from 0 to 1 - depending on animation
    progress.
*/
class Animation
{
public:
    enum AnimFunctions
    {
        LINEAR = 0,
        EASE_OUT_EXPO
    };

    Animation(double duration = 1, double start = 0, double stop = 1);
    void reset();
    void reset(double animation_duration);
    void reset(double animation_duration, double animation_start, double animation_stop);
    double value(AnimFunctions apply_function) const;
    bool done() const;

private:
    static double apply_animation_function(double t, AnimFunctions apply_function);
    std::chrono::high_resolution_clock::time_point start;
    double start_value, end_value, duration;
};
