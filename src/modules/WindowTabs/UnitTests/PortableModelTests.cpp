// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Run without Windows dependencies: c++ -std=c++20 PortableModelTests.cpp -o model-tests
#include "../GroupModel.h"
#include <cassert>
#include <random>
#include <set>
#include <iostream>

int main()
{
    using namespace WindowTabs;
    Group group{ { 1, 2, 3 }, 2 };
    assert(group.Reorder(2, 0));
    assert((group.tabs == std::vector<WindowId>{ 2, 1, 3 }));
    assert(group.active == 2);
    assert(group.Cycle(false) == 3);
    group.Remove(2);
    assert(group.active == 1);
    assert(!group.Select(99));
    assert(!group.Reorder(1, 99));
    assert(!group.Add(0));
    assert(!group.Add(1));
    std::mt19937 random(10);
    for (size_t i = 0; i < 100000; ++i)
    {
        const auto id = random() % 64;
        switch (random() % 6)
        {
        case 0:
            group.Add(id);
            break;
        case 1:
            group.Remove(id);
            break;
        case 2:
            group.Select(id);
            break;
        case 3:
            group.Reorder(id, random() % 64);
            break;
        case 4:
            group.Select(group.Cycle(true));
            break;
        case 5:
            group.Select(group.Cycle(false));
            break;
        }
        const std::set<WindowId> unique(group.tabs.begin(), group.tabs.end());
        assert(unique.size() == group.tabs.size());
        assert(!unique.contains(0));
        assert(group.tabs.size() <= 32);
        assert(group.tabs.empty() ? !group.active : group.Contains(group.active));
    }
    std::cout << "pass: model scenarios and 100000 randomized transitions\n";
}
