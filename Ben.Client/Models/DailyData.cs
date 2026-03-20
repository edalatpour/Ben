using System;
using System.Collections.ObjectModel;

namespace Bennie.Models;

public class DailyData
{
    public string Key { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public ObservableCollection<TaskItem> Tasks { get; set; } = new();
    public ObservableCollection<NoteItem> Notes { get; set; } = new();
}
