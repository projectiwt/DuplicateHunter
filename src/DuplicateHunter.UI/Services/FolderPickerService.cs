using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using DialogResult = System.Windows.Forms.DialogResult;

namespace DuplicateHunter.UI.Services;

public class FolderPickerService
{
    public string? PickFolder()
    {
        using var dialog = new FolderBrowserDialog();

        dialog.Description = "Select a folder to scan";
        dialog.ShowNewFolderButton = false;

        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}