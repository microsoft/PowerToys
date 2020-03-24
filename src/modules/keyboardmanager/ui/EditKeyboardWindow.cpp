#include "pch.h"
#include "EditKeyboardWindow.h"

LRESULT CALLBACK EditKeyboardWindowProc(HWND, UINT, WPARAM, LPARAM);

// This Hwnd will be the window handler for the Xaml Island: A child window that contains Xaml.
HWND hWndXamlIslandEditKeyboardWindow = nullptr;
// This variable is used to check if window registration has been done to avoid repeated registration leading to an error.
bool isEditKeyboardWindowRegistrationCompleted = false;

// Function to create the Edit Keyboard Window
void createEditKeyboardWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState)
{
    // Window Registration
    const wchar_t szWindowClass[] = L"EditKeyboardWindow";
    if (!isEditKeyboardWindowRegistrationCompleted)
    {
        WNDCLASSEX windowClass = {};
        windowClass.cbSize = sizeof(WNDCLASSEX);
        windowClass.lpfnWndProc = EditKeyboardWindowProc;
        windowClass.hInstance = hInst;
        windowClass.lpszClassName = szWindowClass;
        windowClass.hbrBackground = (HBRUSH)(COLOR_WINDOW);
        windowClass.hIconSm = LoadIcon(windowClass.hInstance, IDI_APPLICATION);
        if (RegisterClassEx(&windowClass) == NULL)
        {
            MessageBox(NULL, L"Windows registration failed!", L"Error", NULL);
            return;
        }

        isEditKeyboardWindowRegistrationCompleted = true;
    }

    // Window Creation
    HWND _hWndEditKeyboardWindow = CreateWindow(
        szWindowClass,
        L"PowerKeys Remap Keyboard",
        WS_OVERLAPPEDWINDOW | WS_VISIBLE,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        CW_USEDEFAULT,
        NULL,
        NULL,
        hInst,
        NULL);
    if (_hWndEditKeyboardWindow == NULL)
    {
        MessageBox(NULL, L"Call to CreateWindow failed!", L"Error", NULL);
        return;
    }

    // This DesktopWindowXamlSource is the object that enables a non-UWP desktop application
    // to host UWP controls in any UI element that is associated with a window handle (HWND).
    DesktopWindowXamlSource desktopSource;
    // Get handle to corewindow
    auto interop = desktopSource.as<IDesktopWindowXamlSourceNative>();
    // Parent the DesktopWindowXamlSource object to current window
    check_hresult(interop->AttachToWindow(_hWndEditKeyboardWindow));

    // Get the new child window's hwnd
    interop->get_WindowHandle(&hWndXamlIslandEditKeyboardWindow);
    // Update the xaml island window size becuase initially is 0,0
    SetWindowPos(hWndXamlIslandEditKeyboardWindow, 0, 0, 0, 400, 400, SWP_SHOWWINDOW);

    // Creating the Xaml content. xamlContainer is the parent UI element
    Windows::UI::Xaml::Controls::StackPanel xamlContainer;
    xamlContainer.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::AliceBlue() });

    // Header for the window
    Windows::UI::Xaml::Controls::StackPanel header;
    header.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    header.Margin({ 10, 10, 10, 30 });
    header.Spacing(10);

    // Header text
    TextBlock headerText;
    headerText.Text(winrt::to_hstring("Remap Keyboard"));
    headerText.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    headerText.FontSize(30);
    headerText.Margin({ 0, 0, 1000, 0 });

    // Header Cancel button
    Button cancelButton;
    cancelButton.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    cancelButton.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    cancelButton.Content(winrt::box_value(winrt::to_hstring("Cancel")));

    // Header Apply button
    Button applyButton;
    applyButton.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    applyButton.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    applyButton.Content(winrt::box_value(winrt::to_hstring("Apply")));

    // Table to display the remap keys
    Windows::UI::Xaml::Controls::StackPanel remapTable;
    remapTable.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    remapTable.Margin({ 10, 10, 10, 20 });
    remapTable.Spacing(10);

    // Header row of the remap keys table
    Windows::UI::Xaml::Controls::StackPanel tableHeaderRow;
    tableHeaderRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableHeaderRow.Spacing(100);
    tableHeaderRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // First header textblock in the header row of the remap keys table
    TextBlock originalRemapHeader;
    originalRemapHeader.Text(winrt::to_hstring("Original Key:"));
    originalRemapHeader.FontWeight(Text::FontWeights::Bold());
    originalRemapHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(originalRemapHeader);

    // Second header textblock in the header row of the shortcut table
    TextBlock newRemapHeader;
    newRemapHeader.Text(winrt::to_hstring("New Key:"));
    newRemapHeader.FontWeight(Text::FontWeights::Bold());
    newRemapHeader.Margin({ 0, 0, 0, 10 });
    tableHeaderRow.Children().Append(newRemapHeader);

    remapTable.Children().Append(tableHeaderRow);





    header.Children().Append(headerText);
    header.Children().Append(cancelButton);
    header.Children().Append(applyButton);

    xamlContainer.Children().Append(header);
    xamlContainer.Children().Append(remapTable);
    xamlContainer.UpdateLayout();

    desktopSource.Content(xamlContainer);
    ////End XAML Island section
    if (_hWndEditKeyboardWindow)
    {
        ShowWindow(_hWndEditKeyboardWindow, SW_SHOW);
        UpdateWindow(_hWndEditKeyboardWindow);
    }

    // Message loop:
    MSG msg = {};
    while (GetMessage(&msg, NULL, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    desktopSource.Close();
}

LRESULT CALLBACK EditKeyboardWindowProc(HWND hWnd, UINT messageCode, WPARAM wParam, LPARAM lParam)
{
    RECT rcClient;
    switch (messageCode)
    {
    case WM_PAINT:
        GetClientRect(hWnd, &rcClient);
        SetWindowPos(hWndXamlIslandEditKeyboardWindow, 0, rcClient.left, rcClient.top, rcClient.right, rcClient.bottom, SWP_SHOWWINDOW);
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, messageCode, wParam, lParam);
        break;
    }

    return 0;
}
