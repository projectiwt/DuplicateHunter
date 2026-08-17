using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using Orientation = System.Windows.Controls.Orientation;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;
using Window = System.Windows.Window;

namespace DuplicateHunter.UI.Services;

public class WpfDialogService : IDialogService
{
    public bool Confirm(string message, string title = "Confirmation")
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    public void ShowInformation(string message, string title = "Information")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    public string? PromptInput(string prompt, string title = "Input", string defaultValue = "")
    {
        var inputWindow = new Window
        {
            Title = title,
            Width = 450,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = System.Windows.Media.Brushes.White,
            ShowInTaskbar = false
        };

        var stackPanel = new StackPanel { Margin = new Thickness(20) };
        var label = new TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 10),
            FontWeight = FontWeights.SemiBold
        };
        var textBox = new TextBox
        {
            Text = defaultValue,
            Height = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(5),
            Margin = new Thickness(0, 0, 0, 15)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Height = 32,
            IsDefault = true,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Height = 32,
            IsCancel = true
        };

        bool isOk = false;
        okButton.Click += (s, e) => { isOk = true; inputWindow.Close(); };
        cancelButton.Click += (s, e) => { inputWindow.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        stackPanel.Children.Add(label);
        stackPanel.Children.Add(textBox);
        stackPanel.Children.Add(buttonPanel);

        inputWindow.Content = stackPanel;
        inputWindow.ShowDialog();

        return isOk ? textBox.Text : null;
    }

    public string? SaveFilePicker(string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
