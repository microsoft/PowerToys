/**
 * @file DwellIndicator.h
 * @brief Visual countdown indicator for DwellCursor module
 * 
 * This header defines the interface for the visual feedback system that shows
 * users when a dwell click is about to occur. The indicator appears as a
 * circular progress arc that fills clockwise during the countdown period.
 * 
 * Key Features:
 * - Transparent overlay window that doesn't interfere with normal interaction
 * - System accent color theming for consistency with Windows
 * - DPI-aware rendering for high-resolution displays
 * - Smooth progress animation updated at 30 FPS
 * - Automatic positioning centered on cursor location
 */

#pragma once
#include <memory>

// Forward declaration to hide implementation details (Pimpl idiom)
class DwellIndicatorImpl;

/**
 * @brief Visual countdown indicator for dwell cursor functionality
 * 
 * This class provides a clean interface for showing a circular progress
 * indicator during dwell cursor countdown. It uses the Pimpl (Pointer to
 * Implementation) idiom to hide all Windows/GDI+ dependencies from the header.
 * 
 * Usage Pattern:
 * 1. Create instance: DwellIndicator indicator;
 * 2. Initialize: indicator.Initialize();
 * 3. Show at cursor: indicator.Show(x, y);
 * 4. Update progress: indicator.UpdateProgress(0.5f); // 50% complete
 * 5. Hide when done: indicator.Hide();
 * 6. Cleanup: indicator.Cleanup(); // or let destructor handle it
 * 
 * Thread Safety:
 * - All methods must be called from the same thread (UI thread)
 * - Progress updates can be called frequently (30ms intervals recommended)
 * - Hide/Show calls are safe to call multiple times
 */
class DwellIndicator
{
public:
    /**
     * @brief Constructor - creates implementation instance
     * 
     * Note: This only creates the object, call Initialize() before use.
     */
    DwellIndicator();
    
    /**
     * @brief Destructor - ensures proper cleanup
     * 
     * Automatically calls Cleanup() if not already called.
     */
    ~DwellIndicator();

    /**
     * @brief Initialize the indicator system
     * 
     * Must be called before any other operations. Sets up:
     * - GDI+ graphics system
     * - Transparent overlay window
     * - DPI awareness
     * 
     * @return true if initialization successful, false on failure
     */
    bool Initialize();

    /**
     * @brief Show the indicator at specified screen coordinates
     * 
     * Displays the circular indicator centered on the given position.
     * If already visible, moves to new position. Window is sized
     * automatically based on DPI and indicator radius.
     * 
     * @param x Screen X coordinate in pixels
     * @param y Screen Y coordinate in pixels
     */
    void Show(int x, int y);

    /**
     * @brief Update the countdown progress
     * 
     * Updates the progress arc to show how much of the dwell delay
     * has elapsed. Can be called frequently for smooth animation.
     * 
     * @param progress Progress value from 0.0 (start) to 1.0 (complete)
     *                 Values outside this range are automatically clamped
     */
    void UpdateProgress(float progress);

    /**
     * @brief Hide the indicator
     * 
     * Makes the indicator invisible but keeps resources allocated
     * for potential re-showing. Safe to call multiple times.
     */
    void Hide();

    /**
     * @brief Clean up all resources
     * 
     * Destroys the window, shuts down GDI+, releases all resources.
     * Called automatically by destructor if not called explicitly.
     * After calling this, Initialize() must be called again before reuse.
     */
    void Cleanup();

    // Disable copy constructor and assignment operator
    // The indicator manages Windows resources that shouldn't be copied
    DwellIndicator(const DwellIndicator&) = delete;
    DwellIndicator& operator=(const DwellIndicator&) = delete;

private:
    /**
     * @brief Pointer to implementation (Pimpl idiom)
     * 
     * This hides all Windows/GDI+ implementation details from the header,
     * reducing compile dependencies and keeping the interface clean.
     */
    std::unique_ptr<DwellIndicatorImpl> m_impl;
};