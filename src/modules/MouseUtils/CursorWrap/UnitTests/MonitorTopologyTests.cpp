// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <windows.h>
#include <vector>

#include "..\MonitorTopology.h"
#include "..\CursorWrapCore.h" // For the CursorDirection struct referenced by MonitorTopology

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CursorWrapUnitTests
{
	namespace
	{
		// Fabricate a stable HMONITOR handle from an integer. MonitorTopology resolves handles
		// via a direct comparison against the stored MonitorInfo::hMonitor first, so synthetic
		// handles let us unit-test the topology logic without any real displays attached.
		HMONITOR MakeMonitorHandle(int id)
		{
			return reinterpret_cast<HMONITOR>(static_cast<intptr_t>(id));
		}

		MonitorInfo MakeMonitor(int id, LONG left, LONG top, LONG right, LONG bottom, bool isPrimary = false)
		{
			MonitorInfo info{};
			info.hMonitor = MakeMonitorHandle(id);
			info.rect = { left, top, right, bottom };
			info.isPrimary = isPrimary;
			info.monitorId = id;
			return info;
		}

		// Returns true when the cursor at the given point wraps off the specified edge of the
		// monitor. wantEdge guards against a corner accidentally matching a different edge.
		bool EdgeWraps(const MonitorTopology& topology, HMONITOR monitor, const POINT& cursor,
					   EdgeType wantEdge, bool suppressTopEdgeAtGlobalTop)
		{
			EdgeType edge{};
			bool onOuter = topology.IsOnOuterEdge(monitor, cursor, edge, WrapMode::Both, nullptr,
												  suppressTopEdgeAtGlobalTop);
			return onOuter && edge == wantEdge;
		}

		POINT TopEdgeCenter(const MonitorInfo& m)
		{
			return POINT{ (m.rect.left + m.rect.right) / 2, m.rect.top };
		}

		POINT BottomEdgeCenter(const MonitorInfo& m)
		{
			return POINT{ (m.rect.left + m.rect.right) / 2, m.rect.bottom - 1 };
		}

		POINT LeftEdgeCenter(const MonitorInfo& m)
		{
			return POINT{ m.rect.left, (m.rect.top + m.rect.bottom) / 2 };
		}
	}

	// Verifies the "Suppress top-edge wrap in Remote Desktop sessions" behavior, whose core
	// requirement is that suppression happens only on the monitor(s) at the very top of the
	// vertical stack (global-minimum top), never merely on a monitor that has an outer top edge.
	TEST_CLASS(MonitorTopologyRdpSuppressionTests)
	{
	public:
		TEST_METHOD(SingleMonitor_IsAtGlobalTop)
		{
			MonitorTopology topology;
			auto m0 = MakeMonitor(1, 0, 0, 1920, 1080, true);
			topology.Initialize({ m0 });

			Assert::IsTrue(topology.IsMonitorAtGlobalTop(m0.hMonitor),
						   L"The only monitor must be considered the global top.");
		}

		TEST_METHOD(SingleMonitor_TopWrapsUnlessSuppressed)
		{
			MonitorTopology topology;
			auto m0 = MakeMonitor(1, 0, 0, 1920, 1080, true);
			topology.Initialize({ m0 });

			const POINT top = TopEdgeCenter(m0);

			Assert::IsTrue(EdgeWraps(topology, m0.hMonitor, top, EdgeType::Top, false),
						   L"Top edge should wrap normally when suppression is off.");
			Assert::IsFalse(EdgeWraps(topology, m0.hMonitor, top, EdgeType::Top, true),
							L"Top edge should be suppressed on the global-top monitor when suppression is on.");
		}

		TEST_METHOD(VerticalStack_OnlyTopMonitorSuppressed)
		{
			MonitorTopology topology;
			auto top = MakeMonitor(1, 0, 0, 1920, 1080, true);   // global top (top == 0)
			auto bottom = MakeMonitor(2, 0, 1080, 1920, 2160);   // below the top monitor
			topology.Initialize({ top, bottom });

			Assert::IsTrue(topology.IsMonitorAtGlobalTop(top.hMonitor), L"Upper monitor is global top.");
			Assert::IsFalse(topology.IsMonitorAtGlobalTop(bottom.hMonitor), L"Lower monitor is not global top.");

			// Top monitor: top-edge wrap toggles with the flag.
			const POINT topEdge = TopEdgeCenter(top);
			Assert::IsTrue(EdgeWraps(topology, top.hMonitor, topEdge, EdgeType::Top, false),
						   L"Top monitor should wrap up normally when suppression is off.");
			Assert::IsFalse(EdgeWraps(topology, top.hMonitor, topEdge, EdgeType::Top, true),
							L"Top monitor top-edge wrap should be suppressed when the flag is on.");

			// Bottom monitor: its bottom edge (wrapping up to the top of the stack) must be unaffected.
			const POINT bottomEdge = BottomEdgeCenter(bottom);
			Assert::IsTrue(EdgeWraps(topology, bottom.hMonitor, bottomEdge, EdgeType::Bottom, false),
						   L"Bottom edge should wrap regardless of the flag (control).");
			Assert::IsTrue(EdgeWraps(topology, bottom.hMonitor, bottomEdge, EdgeType::Bottom, true),
						   L"Suppression must not affect the bottom edge of a lower monitor.");
		}

		TEST_METHOD(TwoTopAligned_BothSuppressed)
		{
			MonitorTopology topology;
			auto left = MakeMonitor(1, 0, 0, 1920, 1080, true);      // top == 0
			auto right = MakeMonitor(2, 1920, 0, 3840, 1080);        // top == 0 (also global top)
			topology.Initialize({ left, right });

			Assert::IsTrue(topology.IsMonitorAtGlobalTop(left.hMonitor), L"Left monitor shares the global top.");
			Assert::IsTrue(topology.IsMonitorAtGlobalTop(right.hMonitor), L"Right monitor shares the global top.");

			const POINT leftTop = TopEdgeCenter(left);
			const POINT rightTop = TopEdgeCenter(right);

			Assert::IsTrue(EdgeWraps(topology, left.hMonitor, leftTop, EdgeType::Top, false));
			Assert::IsTrue(EdgeWraps(topology, right.hMonitor, rightTop, EdgeType::Top, false));

			Assert::IsFalse(EdgeWraps(topology, left.hMonitor, leftTop, EdgeType::Top, true),
							L"Both top-aligned monitors are at the global top and must be suppressed.");
			Assert::IsFalse(EdgeWraps(topology, right.hMonitor, rightTop, EdgeType::Top, true),
							L"Both top-aligned monitors are at the global top and must be suppressed.");
		}

		// The critical case: a lower monitor with an *outer* top edge that is nonetheless NOT at the
		// top of the vertical stack must keep wrapping even while suppression is active.
		TEST_METHOD(Staircase_LowerMonitorWithOuterTopStillWraps)
		{
			MonitorTopology topology;
			auto topMon = MakeMonitor(1, 0, 0, 1920, 1080, true);       // global top (top == 0)
			auto stepMon = MakeMonitor(2, 1920, 540, 3840, 1620);       // shifted down; top == 540
			topology.Initialize({ topMon, stepMon });

			Assert::IsTrue(topology.IsMonitorAtGlobalTop(topMon.hMonitor), L"Upper monitor is the global top.");
			Assert::IsFalse(topology.IsMonitorAtGlobalTop(stepMon.hMonitor),
							L"Lower stepped monitor has an outer top edge but is NOT the global top.");

			// Upper monitor is suppressed at the global top.
			const POINT topEdge = TopEdgeCenter(topMon);
			Assert::IsTrue(EdgeWraps(topology, topMon.hMonitor, topEdge, EdgeType::Top, false));
			Assert::IsFalse(EdgeWraps(topology, topMon.hMonitor, topEdge, EdgeType::Top, true),
							L"Global-top monitor must be suppressed.");

			// Lower stepped monitor keeps wrapping whether or not suppression is on.
			const POINT stepTopEdge = TopEdgeCenter(stepMon);
			bool wrapsWithoutFlag = EdgeWraps(topology, stepMon.hMonitor, stepTopEdge, EdgeType::Top, false);
			bool wrapsWithFlag = EdgeWraps(topology, stepMon.hMonitor, stepTopEdge, EdgeType::Top, true);
			Assert::IsTrue(wrapsWithoutFlag, L"Stepped monitor should wrap off its outer top edge.");
			Assert::AreEqual(wrapsWithoutFlag, wrapsWithFlag,
							 L"Suppression must not affect a monitor that is not at the global top.");
		}

		TEST_METHOD(HorizontalEdges_Unaffected)
		{
			MonitorTopology topology;
			auto top = MakeMonitor(1, 0, 0, 1920, 1080, true);
			auto bottom = MakeMonitor(2, 0, 1080, 1920, 2160);
			topology.Initialize({ top, bottom });

			// The left edge of the global-top monitor must wrap horizontally regardless of the flag.
			const POINT leftEdge = LeftEdgeCenter(top);
			Assert::IsTrue(EdgeWraps(topology, top.hMonitor, leftEdge, EdgeType::Left, false));
			Assert::IsTrue(EdgeWraps(topology, top.hMonitor, leftEdge, EdgeType::Left, true),
						   L"Horizontal wrapping must never be affected by top-edge suppression.");
		}
	};
}
