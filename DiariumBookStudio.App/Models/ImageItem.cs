using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.IO;

namespace DiariumBookStudio.Models;

public class ImageItem : INotifyPropertyChanged
{
    private bool _selected;
    private string _role = "Galerie";
    private string _caption = string.Empty;
    private int _order;

    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(RelativePath);

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public string Role
    {
        get => _role;
        set
        {
            if (SetField(ref _role, value))
                OnPropertyChanged(nameof(IsMainImage));
        }
    }

    public string Caption
    {
        get => _caption;
        set => SetField(ref _caption, value);
    }

    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    public bool IsMainImage
    {
        get => string.Equals(Role, "Hauptbild", System.StringComparison.OrdinalIgnoreCase);
        set
        {
            if (value)
            {
                Role = "Hauptbild";
                OnPropertyChanged();
            }
            else if (IsMainImage)
            {
                Role = "Galerie";
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
