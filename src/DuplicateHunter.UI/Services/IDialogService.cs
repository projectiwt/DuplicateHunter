namespace DuplicateHunter.UI.Services;

public interface IDialogService
{
    bool Confirm(string message, string title = "Confirmation");
    void ShowInformation(string message, string title = "Information");
    void ShowWarning(string message, string title = "Warning");
    void ShowError(string message, string title = "Error");
    string? PromptInput(string prompt, string title = "Input", string defaultValue = "");
    string? SaveFilePicker(string defaultFileName, string filter);
}
