// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Storage;
using Windows.Storage.Pickers;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FilePickerParameterRun : CommandParameterRun
{
    public static readonly IconInfo AddIcon = new("\uE710"); // Add

    public StorageFile? File { get; private set; }

    public override object? Value => File;

    public override string? DisplayText
    {
        get => File != null ?
            File.Name :
            Properties.Resources.FilePickerParameterRun_PlaceholderText;
    }

    public Action<FileOpenPicker>? SetupFilePicker { get; set; }

    public FilePickerParameterRun()
    {
        var command = new FilePickerCommand();
        command.FileSelected += (s, file) =>
        {
            File = file;
            OnPropertyChanged(nameof(NeedsValue));
            OnPropertyChanged(nameof(DisplayText));
        };
        command.RequestCustomizePicker += ConfigureFilePicker;
        PlaceholderText = Properties.Resources.FilePickerParameterRun_PlaceholderText;
        Icon = AddIcon;
        Command = command;
    }

    public override void ClearValue()
    {
        File = null;
    }

    private sealed partial class FilePickerCommand : InvokableCommand, IRequiresHostHwnd
    {
        public override IconInfo Icon => FilePickerParameterRun.AddIcon;

        public override string Name => Properties.Resources.FilePickerParameterRun_PlaceholderText;

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
