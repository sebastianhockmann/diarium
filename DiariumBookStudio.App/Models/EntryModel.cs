using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;

namespace DiariumBookStudio.Models;

public class EntryModel : INotifyPropertyChanged
{
    private string _editedText = string.Empty;
    private string _placeName = string.Empty;
    private bool _done;

    /// <summary>
    /// Stabiler Schlüssel (Datum + laufende Nummer am Tag). Wird für Dateinamen
    /// und zum Abgleich bei einem erneuten Import verwendet.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public DateTime? Date { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Weather { get; set; } = string.Empty;
    public int Rating { get; set; }
    public List<string> Tags { get; set; } = new();
    public ObservableCollection<ImageItem> Images { get; set; } = new();

    public string EditedText
    {
        get => _editedText;
        set => SetField(ref _editedText, value);
    }

    /// <summary>Lesbarer Ortsname, z. B. „Gera“. Wird beim Export per Nominatim ermittelt, falls leer.</summary>
    public string PlaceName
    {
        get => _placeName;
        set => SetField(ref _placeName, value);
    }

    public bool Done
    {
        get => _done;
        set => SetField(ref _done, value);
    }

    [JsonIgnore]
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
