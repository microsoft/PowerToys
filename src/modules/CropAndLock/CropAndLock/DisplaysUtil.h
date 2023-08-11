#pragma once

inline RECT ComputeAllDisplaysUnion(std::vector<robmikh::common::desktop::DisplayInfo> const& infos)
{
    RECT result = {};
    result.left = LONG_MAX;
    result.top = LONG_MAX;
    result.right = LONG_MIN;
    result.bottom = LONG_MIN;
    for (auto&& info : infos)
    {
        auto rect = info.Rect();
        result.left = std::min(result.left, rect.left);
        result.top = std::min(result.top, rect.top);
        result.right = std::max(result.right, rect.right);
        result.bottom = std::max(result.bottom, rect.bottom);
    }
    return result;
}

inline RECT ComputeAllDisplaysUnion()
{
    auto infos = robmikh::common::desktop::DisplayInfo::GetAllDisplays();
    return ComputeAllDisplaysUnion(infos);
}
