using System;
using System.Collections.ObjectModel;

namespace Ben.Models;

public class DailyData
{
    public DateTime Key { get; set; }
    public DateTime Date { get; set; }
    public ObservableCollection<TaskItem> Tasks { get; set; } = new();
    public ObservableCollection<NoteItem> Notes { get; set; } = new();
}
