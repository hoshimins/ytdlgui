using Microsoft.Win32;

namespace YtdlGUI.Wpf.Services;

public sealed class WindowsFolderPicker : IFolderPicker
{
    public string? PickFolder(string initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "保存先フォルダを選択",
            InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : null,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
