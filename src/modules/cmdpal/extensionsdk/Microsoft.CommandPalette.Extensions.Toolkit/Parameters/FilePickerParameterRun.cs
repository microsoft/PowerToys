// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Storage;
using Windows.Storage.Pickers;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FilePickerParameterRun : CommandParameterRun
{
    public StorageFile? File { get; private set; }

    public override object? Value => File;

    public override string? DisplayText { get => File != null ? File.DisplayName : "Select a file"; }

    public Action<FileOpenPicker>? SetupFilePicker { get; set; }

    public FilePickerParameterRun()
    {
        var command = new FilePickerCommand();
        command.FileSelected += (s, file) =>
        {
            File = file;

            // Value = file != null ? file : (object?)null;
            // OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(NeedsValue));
            OnPropertyChanged(nameof(DisplayText));
        };
        command.RequestCustomizePicker += ConfigureFilePicker;
        PlaceholderText = "Select a file";
        Icon = new IconInfo("\uE710"); // Add
        Command = command;
    }

    public override void ClearValue()
    {
        File = null;
    }

    private sealed partial class FilePickerCommand : InvokableCommand, IRequiresHostHwnd
    {
        public override IconInfo Icon => new("\uE710"); // Add

        public override string Name => "Pick a file";

        public event EventHandler<StorageFile?>? FileSelected;

        public event EventHandler<FileOpenPicker>? RequestCustomizePicker;

        private nint _hostHwnd;

        public void SetHostHwnd(nint hostHwnd)
        {
            _hostHwnd = hostHwnd;
        }

        public override ICommandResult Invoke()
        {
            PickFileAsync();
            return CommandResult.KeepOpen();
        }

        private async void PickFileAsync()
        {
            var picker = new FileOpenPicker() { };

            RequestCustomizePicker?.Invoke(this, picker);

            // You need to initialize the picker with a window handle in WinUI 3 desktop apps
            // See https://learn.microsoft.com/en-us/windows/apps/design/controls/file-open-picker
            WinRT.Interop.InitializeWithWindow.Initialize(picker, (nint)_hostHwnd);

            var file = await picker.PickSingleFileAsync();
            FileSelected?.Invoke(this, file);
        }
    }

    protected virtual void ConfigureFilePicker(object? sender, FileOpenPicker picker)
    {
        picker.FileTypeFilter.Add("*");
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
