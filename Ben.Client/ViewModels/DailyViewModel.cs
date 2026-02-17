// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Ben.Models;
using Ben.Services;

namespace Ben.ViewModels;

public class DailyViewModel : INotifyPropertyChanged
{
    static readonly string[] PriorityOrder = new[] { "A", "B", "C" };
    public event PropertyChangedEventHandler PropertyChanged;

    private readonly PlannerRepository _repo;

    public DailyViewModel(PlannerRepository repo)
    {
        _repo = repo;
        DateTime key = DateTime.Today;
        LoadDay(key);
    }

    DailyData _currentDay;
    public DailyData CurrentDay
    {
        get => _currentDay;
        set { _currentDay = value; OnPropertyChanged(); }
    }

    int _subPage = 0; // 0 = tasks, 1 = notes
    public int SubPage
    {
        get => _subPage;
        set { _subPage = value; OnPropertyChanged(); }
    }

    public async Task LoadDay(DateTime key)
    {
        DailyData day = await _repo.LoadDayAsync(key);
        // day.Tasks.Add(new TaskItem { Status = "I", Priority = "A", Order = 1, Title = "The most important thing" });
        // day.Tasks.Add(new TaskItem { Status = "C", Priority = "A", Order = 2, Title = "Also important" });
        // day.Tasks.Add(new TaskItem { Status = "I", Priority = "A", Order = 1, Title = "The most important thing" });
        // day.Notes.Add(new NoteItem { Text = "I like this!"});
        EnsurePriorityBuckets(day);
        EnsurePlaceholderNote(day);
        CurrentDay = day;
        // CurrentDay = new DailyData
        // {
        //     Date = date,
        //     Tasks = new List<TaskItem>
        //     {
        //         new TaskItem { Status = "I", Priority = "A", Order = 1, Title = "The most important thing" },
        //         new TaskItem { Status = "C", Priority = "A", Order = 2, Title = "Also important" },
        //         new TaskItem { Status = " ", Priority = "B", Order = 1, Title = "Nice to have" },
        //         new TaskItem { Status = " ", Priority = "C", Order = 1, Title = "No big deal" }
        //     },
        //     Notes = new List<NoteItem>
        //     {
        //         new NoteItem{ Note = "I just thought of something." },
        //         new NoteItem{ Note = "Today is " + date.ToString("yyyy-MM-dd") + "." },
        //         new NoteItem{ Note = "I like turtles!" }
        //     }
        // };
    }

    public async Task AddTaskAsync(string text)
    {
        text = NormalizeTaskTitle(text);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var task = new TaskItem
        {
            Key = CurrentDay.Key,
            // Status = StatusEnum.NotStarted,
            Status = "NotStarted",
            Priority = "",
            Order = GetNextTaskOrder(),
            Title = text
        };

        await _repo.AddTaskAsync(task);
        InsertTaskBeforePlaceholder(task);
        EnsurePriorityBuckets(CurrentDay);
    }

    static string NormalizeTaskTitle(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\u00A0", " ")
            .Replace("\u200B", " ")
            .Replace("\uFEFF", " ")
            .Trim();
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        await _repo.UpdateTaskAsync(task);
    }

    public async Task ReorderTaskAsync(TaskItem source, TaskItem target)
    {
        if (source == null || target == null || source.IsPlaceholder || source.IsPriorityBucket || target.IsAddPlaceholder)
        {
            return;
        }

        var tasks = CurrentDay?.Tasks;
        if (tasks == null || tasks.Count == 0)
        {
            return;
        }

        int sourceIndex = tasks.IndexOf(source);
        int targetIndex = tasks.IndexOf(target);
        int lastIndex = GetLastTaskIndex(tasks);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        if (sourceIndex > lastIndex)
        {
            return;
        }

        if (targetIndex > lastIndex)
        {
            targetIndex = lastIndex;
        }

        tasks.Move(sourceIndex, targetIndex);

        if (!string.Equals(source.Priority, target.Priority, StringComparison.Ordinal))
        {
            source.Priority = target.Priority;
            await _repo.UpdateTaskAsync(source);
        }

        await UpdateTaskOrderAsync();
        EnsurePriorityBuckets(CurrentDay);
    }

    public async Task DeleteNoteAsync(TaskItem task)
    {
        if (task.IsPlaceholder || task.IsPriorityBucket)
        {
            return;
        }

        await _repo.DeleteTaskAsync(task);
        CurrentDay.Tasks.Remove(task);
        EnsurePriorityBuckets(CurrentDay);
    }

    public async Task AddNoteAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var note = new NoteItem
        {
            Key = CurrentDay.Key,
            Text = text,
            Order = GetNextNoteOrder()
        };

        await _repo.AddNoteAsync(note);
        InsertNoteBeforePlaceholder(note);
        EnsurePlaceholderNote(CurrentDay);
    }

    public async Task UpdateNoteAsync(NoteItem note)
    {
        await _repo.UpdateNoteAsync(note);
    }

    public async Task DeleteNoteAsync(NoteItem note)
    {
        if (note.IsPlaceholder)
        {
            return;
        }

        await _repo.DeleteNoteAsync(note);
        CurrentDay.Notes.Remove(note);
        EnsurePlaceholderNote(CurrentDay);
    }

    public async Task GoForwardAsync()
    {
        if (CurrentDay == null)
        {
            return;
        }

        if (SubPage == 0)
        {
            SubPage = 1;
            return;
        }

        SubPage = 0;
        await LoadDay(CurrentDay.Key.AddDays(1));
    }

    public async Task GoBackwardAsync()
    {
        if (CurrentDay == null)
        {
            return;
        }

        if (SubPage == 1)
        {
            SubPage = 0;
            return;
        }

        SubPage = 1;
        await LoadDay(CurrentDay.Key.AddDays(-1));
    }

    void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    void EnsurePlaceholderNote(DailyData day)
    {
        if (day?.Notes == null)
        {
            return;
        }

        if (day.Notes.Count == 0 || !day.Notes[^1].IsPlaceholder)
        {
            day.Notes.Add(new NoteItem
            {
                Key = day.Key,
                Text = string.Empty,
                Order = int.MaxValue,
                IsPlaceholder = true
            });
        }
    }

    int GetNextNoteOrder()
    {
        int order = 1;
        foreach (NoteItem note in CurrentDay.Notes)
        {
            if (!note.IsPlaceholder)
            {
                order++;
            }
        }

        return order;
    }

    void EnsurePlaceholderTask(DailyData day)
    {
        if (day?.Tasks == null)
        {
            return;
        }

        var tasks = day.Tasks;
        for (int i = tasks.Count - 1; i >= 0; i--)
        {
            if (tasks[i].IsAddPlaceholder)
            {
                tasks.RemoveAt(i);
            }
        }

        tasks.Add(new TaskItem
        {
            Key = day.Key,
            Status = "NotStarted",
            Priority = "A",
            Order = int.MaxValue,
            Title = string.Empty,
            IsPlaceholder = true,
            IsAddPlaceholder = true
        });
    }

    void InsertTaskBeforePlaceholder(TaskItem task)
    {
        var tasks = CurrentDay.Tasks;
        int placeholderIndex = -1;
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].IsPlaceholder)
            {
                placeholderIndex = i;
                break;
            }
        }

        if (placeholderIndex >= 0)
        {
            tasks.Insert(placeholderIndex, task);
        }
        else
        {
            tasks.Add(task);
        }
    }

    int GetLastTaskIndex(IList<TaskItem> tasks)
    {
        int lastIndex = tasks.Count - 1;
        while (lastIndex >= 0 && (tasks[lastIndex].IsPlaceholder || tasks[lastIndex].IsPriorityBucket))
        {
            lastIndex--;
        }

        return lastIndex;
    }

    int GetNextTaskOrder()
    {
        int order = 1;
        foreach (TaskItem task in CurrentDay.Tasks)
        {
            if (!task.IsPlaceholder)
            {
                order++;
            }
        }

        return order;
    }

    async Task UpdateTaskOrderAsync()
    {
        var orderByPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (TaskItem task in CurrentDay.Tasks)
        {
            if (task.IsPlaceholder || task.IsPriorityBucket)
            {
                continue;
            }

            string priorityKey = task.Priority ?? string.Empty;
            orderByPriority.TryGetValue(priorityKey, out int current);
            int nextOrder = current + 1;
            orderByPriority[priorityKey] = nextOrder;

            if (task.Order != nextOrder)
            {
                task.Order = nextOrder;
                await _repo.UpdateTaskAsync(task);
            }
        }
    }

    void EnsurePriorityBuckets(DailyData day)
    {
        if (day?.Tasks == null)
        {
            return;
        }

        var tasks = day.Tasks;
        var addPlaceholder = tasks.FirstOrDefault(task => task.IsAddPlaceholder);
        var realTasks = tasks
            .Where(task => !task.IsAddPlaceholder && !task.IsPriorityBucket)
            .ToList();

        var rebuilt = new List<TaskItem>();

        foreach (string priority in PriorityOrder)
        {
            bool hasTasksForPriority = realTasks.Any(task =>
                string.Equals(task.Priority, priority, StringComparison.OrdinalIgnoreCase));

            if (!hasTasksForPriority)
            {
                rebuilt.Add(new TaskItem
                {
                    Key = day.Key,
                    Status = "NotStarted",
                    Priority = priority,
                    Order = int.MaxValue,
                    Title = string.Empty,
                    IsPriorityBucket = true
                });
            }

            foreach (TaskItem task in realTasks)
            {
                if (string.Equals(task.Priority, priority, StringComparison.OrdinalIgnoreCase))
                {
                    rebuilt.Add(task);
                }
            }
        }

        foreach (TaskItem task in realTasks)
        {
            if (!PriorityOrder.Any(priority => string.Equals(task.Priority, priority, StringComparison.OrdinalIgnoreCase)))
            {
                rebuilt.Add(task);
            }
        }

        tasks.Clear();
        foreach (TaskItem task in rebuilt)
        {
            tasks.Add(task);
        }

        if (addPlaceholder == null)
        {
            EnsurePlaceholderTask(day);
        }
        else
        {
            tasks.Add(addPlaceholder);
        }
    }

    void InsertNoteBeforePlaceholder(NoteItem note)
    {
        var notes = CurrentDay.Notes;
        int placeholderIndex = -1;
        for (int i = 0; i < notes.Count; i++)
        {
            if (notes[i].IsPlaceholder)
            {
                placeholderIndex = i;
                break;
            }
        }

        if (placeholderIndex >= 0)
        {
            notes.Insert(placeholderIndex, note);
        }
        else
        {
            notes.Add(note);
        }
    }
}

// public partial class DailyViewModel(AppDbContext context, IAlertService alertService) : ObservableRecipient
// {
//     [ObservableProperty]
//     public partial bool IsRefreshing { get; set; }

//     [ObservableProperty]
//     public partial ConcurrentObservableCollection<TaskItem> Items { get; set; } = [];

//     [RelayCommand]
//     public async Task RefreshItemsAsync(CancellationToken cancellationToken = default)
//     {
//         if (IsRefreshing)
//         {
//             return;
//         }

//         try
//         {
//             await context.SynchronizeAsync(cancellationToken);
//             List<TaskItem> items = await context.TaskItems.ToListAsync(cancellationToken);
//             Items.ReplaceAll(items);
//         }
//         catch (Exception ex)
//         {
//             await alertService.ShowErrorAlertAsync("RefreshItems", ex.Message);
//         }
//         finally
//         {
//             IsRefreshing = false;
//         }
//     }

//     [RelayCommand]
//     public async Task UpdateItemAsync(string itemId, CancellationToken cancellationToken = default)
//     {
//         try
//         {
//             // TaskItem? item = await context.TaskItems.FindAsync([itemId], cancellationToken);
//             // if (item is not null)
//             // {
//             //     item.Status = !item.Status;
//             //     _ = context.TaskItems.Update(item);
//             //     _ = Items.ReplaceIf(x => x.Id == itemId, item);
//             //     _ = await context.SaveChangesAsync(cancellationToken);
//             // }
//         }
//         catch (Exception ex)
//         {
//             await alertService.ShowErrorAlertAsync("UpdateItem", ex.Message);
//         }
//     }

//     [RelayCommand]
//     public async Task AddItemAsync(string text, CancellationToken cancellationToken = default)
//     {
//         try
//         {
//             TaskItem item = new() { Title = text };
//             _ = context.TaskItems.Add(item);
//             _ = await context.SaveChangesAsync(cancellationToken);
//             Items.Add(item);
//         }
//         catch (Exception ex)
//         {
//             await alertService.ShowErrorAlertAsync("AddItem", ex.Message);
//         }
//     }
// }
