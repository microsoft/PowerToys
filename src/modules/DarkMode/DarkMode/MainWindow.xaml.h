#pragma once
#include "MainWindow.g.h"

namespace winrt::PowerToys::DarkMode::implementation
{
  struct MainWindow : MainWindowT<MainWindow>
  {
    MainWindow();

    void ApplyButton_Click(
      winrt::Windows::Foundation::IInspectable const& sender,
      winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);

    void ModeRadio_Checked(
      winrt::Windows::Foundation::IInspectable const& sender,
      winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);

    void GetLocation_Click(
      winrt::Windows::Foundation::IInspectable const& sender,
      winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);

    void ForceLight_Click(
      winrt::Windows::Foundation::IInspectable const& sender,
      winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);

    void ForceDark_Click(
      winrt::Windows::Foundation::IInspectable const& sender,
      winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);

    void UpdateSunriseSunset();

  private:
    // Store user-selected times
    Windows::Foundation::TimeSpan lightTime{};
    Windows::Foundation::TimeSpan darkTime{};

    // Persistent timer
    Microsoft::UI::Dispatching::DispatcherQueueTimer m_timer{ nullptr };

    double m_latitude = 0.0;
    double m_longitude = 0.0;

    void ScheduleNextTick();
    void OnTimerTick(IInspectable const&, IInspectable const&);
  };
}

namespace winrt::PowerToys::DarkMode::factory_implementation
{
  struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
  {
  };
}
