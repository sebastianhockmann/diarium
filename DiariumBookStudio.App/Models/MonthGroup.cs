using System.Collections.ObjectModel;

namespace DiariumBookStudio.Models;

public class MonthGroup
{
    public string Title { get; set; } = string.Empty;
    public ObservableCollection<EntryModel> Entries { get; set; } = new();
}
