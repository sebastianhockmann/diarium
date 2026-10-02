using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;

namespace DiariumBookStudio.Models;

public class EntryModel : INotifyPropertyChanged
{
    private string _editedText = string.Empty;
    private bool _done;

    public DateTime? Date { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
    public ObservableCollection<ImageItem> Images { get; set; } = new();

    public string EditedText
    {
        get => _editedText;
        set => SetField(ref _editedText, value);
    }

    public bool Done
    {
        get => _done;
        set => SetField(ref _done, value);
    }

    public string DisplayTitle
    {
        get
        {
            var date = Date?.ToString("dd.MM.yyyy") ?? DateText;
            var title = string.IsNullOrWhiteSpace(Title) ? "Ohne Titel" : Title;
            return $"{date} – {title} ({Images.Count} Bilder)";
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
