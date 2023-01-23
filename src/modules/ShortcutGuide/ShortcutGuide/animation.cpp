#include "pch.h"
#include "animation.h"

Animation::Animation(double duration, double start, double stop) :
    duration(duration), start_value(start), end_value(stop), start(std::chrono::high_resolution_clock::now()) {}

void Animation::reset()
{
    start = std::chrono::high_resolution_clock::now();
}
void Animation::reset(double animation_duration)
{
    duration = animation_duration;
    reset();
}
void Animation::reset(double animation_duration, double animation_start, double animation_stop)
{
    start_value = animation_start;
    end_value = animation_stop;
    reset(animation_duration);
}

static double ease_out_expo(double t)
{
    return 1 - pow(2, -8 * t);
}

double Animation::apply_animation_function(double t, AnimFunctions apply_function)
{
    switch (apply_function)
    {
    case EASE_OUT_EXPO:
        return ease_out_expo(t);
    case LINEAR:
    default:
        return t;
    }
}

double Animation::value(AnimFunctions apply_function) const
{
    auto anim_duration = std::chrono::high_resolution_clock::now() - start;
    double t = std::chrono::duration<double>(anim_duration).count() / duration;
    if (t >= 1)
        return end_value;
    return start_value + (end_value - start_value) * apply_animation_function(t, apply_function);
}
bool Animation::done() const
{
    return std::chrono::high_resolution_clock::now() - start >= std::chrono::duration<double>(duration);
}
