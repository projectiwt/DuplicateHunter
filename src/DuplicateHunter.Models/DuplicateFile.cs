using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace DuplicateHunter.Models;

public class DuplicateFile : INotifyPropertyChanged
{
    private bool _isSelected;

    public string FilePath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public long Size { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();

    public string DirectoryName => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}