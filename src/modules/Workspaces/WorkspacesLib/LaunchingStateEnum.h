#pragma once

// sync with WorkspacesLauncherUI : Data : LaunchingState.cs
enum class LaunchingState
{
	Waiting = 0,
	Launched,
	LaunchedAndMoved,
	Failed,
	Canceled,
};