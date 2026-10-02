using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.IO;

namespace DiariumBookStudio.Models;

public class ImageItem : INotifyPropertyChanged
{
    private bool _selected;
    private bool _isHero;
    private string _caption = string.Empty;
    private int _order;

    /// <summary>Pfad relativ zur Diarium-HTML-Datei, z. B. „media/2026-05-01_…/IMG_6210.jpg“.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Absoluter Pfad. Wird beim Laden aus Projektordner und RelativePath neu berechnet.</summary>
    [JsonIgnore]
    public string FullPath { get; set; } = string.Empty;

    public string FileName => Path.GetFileName(RelativePath);

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    /// <summary>Titelbild: wird groß über dem Eintragstext gesetzt.</summary>
    public bool IsHero
    {
        get => _isHero;
        set => SetField(ref _isHero, value);
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
