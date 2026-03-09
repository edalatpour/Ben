// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Networking;
using Ben.Models;
using Ben.Services;

namespace Ben.ViewModels;

public class DailyViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private readonly PlannerRepository _repo;
    private readonly AuthenticationService _authService;
    private readonly DatasyncSyncService _syncService;
    private readonly IConnectivity _connectivity;
    private bool _isSyncing;

    public DailyViewModel(PlannerRepository repo, AuthenticationService authService, DatasyncSyncService syncService, IConnectivity connectivity)
    {
        _repo = repo;
        _authService = authService;
        _syncService = syncService;
        _connectivity = connectivity;

        DateTime key = DateTime.Today;
        LoadDay(key);

        // Subscribe to events
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        _syncService.SyncStarted += OnSyncStarted;
        _syncService.SyncCompleted += OnSyncCompleted;

        // Initial update
        _ = UpdateStatus();
    }

    private void OnConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
    {
        _ = UpdateStatus();
    }

    private void OnAuthenticationStateChanged(object sender, EventArgs e)
    {
        _ = UpdateStatus();
    }

    private void OnSyncStarted(object sender, EventArgs e)
    {
        _isSyncing = true;
        _ = UpdateStatus();
    }

    private void OnSyncCompleted(object sender, EventArgs e)
    {
        _isSyncing = false;
        _ = UpdateStatus();
    }

    private string _loginStatusText = "Sign in";
    public string LoginStatusText
    {
        get => _loginStatusText;
        set { _loginStatusText = value; OnPropertyChanged(); }
    }

    private string _syncStatusText = "No connectivity";
    public string SyncStatusText
    {
        get => _syncStatusText;
        set { _syncStatusText = value; OnPropertyChanged(); }
    }

    private bool _isOnline = false;
    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(); }
    }

    private bool _isSyncClickable = false;
    public bool IsSyncClickable
    {
        get => _isSyncClickable;
        set { _isSyncClickable = value; OnPropertyChanged(); }
    }

    private async Task UpdateStatus()
    {
        // Update online status
        IsOnline = _connectivity.NetworkAccess == NetworkAccess.Internet;

        // Update sync clickable state (only clickable when authenticated AND online)
        IsSyncClickable = _authService.IsAuthenticated && IsOnline;

        // Update login status
        if (_authService.IsAuthenticated)
        {
            LoginStatusText = _authService.UserEmail ?? "Signed in";
        }
        else
        {
            LoginStatusText = "Sign in";
        }

        // Update sync status
        if (_isSyncing)
        {
            SyncStatusText = "Synchronizing...";
        }
        else if (_connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            var pendingCount = await _syncService.GetUnsyncedChangesCountAsync();
            if (pendingCount > 0)
            {
                SyncStatusText = pendingCount == 1 ? "1 pending change" : $"{pendingCount} pending changes";
            }
            else
            {
                SyncStatusText = "No connectivity";
            }
        }
        else if (!_authService.IsAuthenticated)
        {
            var pendingCount = await _syncService.GetUnsyncedChangesCountAsync();
            if (pendingCount > 0)
            {
                SyncStatusText = pendingCount == 1 ? "1 pending change" : $"{pendingCount} pending changes";
            }
            else
            {
                SyncStatusText = "Not signed in";
            }
        }
        else
        {
            var pendingCount = await _syncService.GetUnsyncedChangesCountAsync();
            if (pendingCount > 0)
            {
                SyncStatusText = pendingCount == 1 ? "1 pending change" : $"{pendingCount} pending changes";
            }
            else
            {
                SyncStatusText = "Up to date";
            }
        }
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
        CurrentDay.Tasks.Add(task);
        SortTasksInMemory();
        await UpdateStatus();
    }

    public async Task AddTaskItemAsync(TaskItem task)
    {
        if (string.IsNullOrWhiteSpace(task.Title))
        {
            return;
        }

        task.Key = CurrentDay.Key;
        task.Priority = string.IsNullOrWhiteSpace(task.Priority) ? "A" : task.Priority;
        if (task.Order <= 0)
        {
            task.Order = GetNextTaskOrder();
        }

        await _repo.AddTaskAsync(task);
        CurrentDay.Tasks.Add(task);
        await ApplyTaskPlacementAsync(task, task.Priority, task.Order);
        await UpdateStatus();
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
        SortTasksInMemory();
        await UpdateStatus();
    }

    public async Task UpdateTaskFromDetailsAsync(TaskItem task, string title, string status, string priority, int order)
    {
        if (task == null)
        {
            return;
        }

        string normalizedTitle = NormalizeTaskTitle(title);
        if (string.IsNullOrEmpty(normalizedTitle))
        {
            return;
        }

        string requestedStatus = string.IsNullOrWhiteSpace(status) ? "NotStarted" : status;
        string requestedPriority = string.IsNullOrWhiteSpace(priority) ? "A" : priority;
        int requestedOrder = Math.Max(1, order);

        task.Title = normalizedTitle;
        task.Status = requestedStatus;

        if (CurrentDay?.Tasks == null || CurrentDay.Tasks.Count == 0 || CurrentDay.Tasks.IndexOf(task) < 0)
        {
            await SaveTaskDirectAsync(task, requestedPriority, requestedOrder);
        }
        else
        {
            await ApplyTaskPlacementAsync(task, requestedPriority, requestedOrder);
        }

        await UpdateStatus();
    }

    public async Task ReorderTaskAsync(TaskItem source, TaskItem target)
    {
        if (source == null || target == null)
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
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        int requestedOrder = sourceIndex < targetIndex ? target.Order + 1 : target.Order;
        string requestedPriority = string.IsNullOrWhiteSpace(target.Priority) ? "A" : target.Priority;
        await ApplyTaskPlacementAsync(source, requestedPriority, requestedOrder);
        await UpdateStatus();
    }

    async Task ApplyTaskPlacementAsync(TaskItem task, string priority, int order)
    {
        if (task == null || CurrentDay?.Tasks == null)
        {
            return;
        }

        string requestedPriority = string.IsNullOrWhiteSpace(priority) ? "A" : priority;
        int requestedOrder = Math.Max(1, order);

        PlaceTaskInMemory(task, requestedPriority, requestedOrder);
        await UpdateTaskOrderAsync(new[] { task });
        SortTasksInMemory();
    }

    async Task SaveTaskDirectAsync(TaskItem task, string priority, int order)
    {
        task.Priority = string.IsNullOrWhiteSpace(priority) ? "A" : priority;
        task.Order = Math.Max(1, order);
        await _repo.UpdateTaskAsync(task);
        SortTasksInMemory();
    }

    void PlaceTaskInMemory(TaskItem task, string requestedPriority, int requestedOrder)
    {
        task.Priority = requestedPriority;

        List<TaskItem> orderedWithoutTask = CurrentDay.Tasks
            .Where(candidate => !ReferenceEquals(candidate, task))
            .OrderBy(candidate => GetPriorityRank(candidate.Priority))
            .ThenBy(candidate => candidate.Order)
            .ThenBy(candidate => candidate.Id)
            .ToList();

        List<int> targetPriorityIndexes = orderedWithoutTask
            .Select((candidate, index) => (candidate, index))
            .Where(tuple => string.Equals(tuple.candidate.Priority, requestedPriority, StringComparison.Ordinal))
            .Select(tuple => tuple.index)
            .ToList();

        int insertionIndex;
        if (targetPriorityIndexes.Count > 0)
        {
            int insertionPositionInPriority = Math.Min(requestedOrder, targetPriorityIndexes.Count + 1) - 1;
            insertionIndex = insertionPositionInPriority >= targetPriorityIndexes.Count
                ? targetPriorityIndexes[^1] + 1
                : targetPriorityIndexes[insertionPositionInPriority];
        }
        else
        {
            int targetRank = GetPriorityRank(requestedPriority);
            insertionIndex = orderedWithoutTask.FindIndex(candidate => GetPriorityRank(candidate.Priority) > targetRank);
            if (insertionIndex < 0)
            {
                insertionIndex = orderedWithoutTask.Count;
            }
        }

        orderedWithoutTask.Insert(insertionIndex, task);

        CurrentDay.Tasks.Clear();
        foreach (TaskItem item in orderedWithoutTask)
        {
            CurrentDay.Tasks.Add(item);
        }
    }

    public async Task DeleteNoteAsync(TaskItem task)
    {
        await _repo.DeleteTaskAsync(task);
        CurrentDay.Tasks.Remove(task);
        SortTasksInMemory();
        await UpdateStatus();
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
        CurrentDay.Notes.Add(note);
        await UpdateStatus();
    }

    public async Task UpdateNoteAsync(NoteItem note)
    {
        await _repo.UpdateNoteAsync(note);
        await UpdateStatus();
    }

    public async Task DeleteNoteAsync(NoteItem note)
    {
        await _repo.DeleteNoteAsync(note);
        CurrentDay.Notes.Remove(note);
        await UpdateStatus();
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

    public async Task ToggleAuthenticationAsync()
    {
        if (_authService.IsAuthenticated)
        {
            await _authService.SignOutWithCleanupAsync(_syncService);
            await UpdateStatus();
            return;
        }

        var result = await _authService.SignInAsync();
        if (result != null)
        {
            _ = _syncService.TrySyncNowAsync();
        }

        await UpdateStatus();
    }

    public async Task ForceSyncAsync()
    {
        if (!_authService.IsAuthenticated)
        {
            return;
        }

        if (_isSyncing)
        {
            return;
        }

        try
        {
            _isSyncing = true;
            await UpdateStatus();

            var success = await _syncService.TrySyncNowAsync();

            if (success)
            {
                // Reload current day to show synced data
                await LoadDay(CurrentDay.Key);
            }
        }
        finally
        {
            _isSyncing = false;
            await UpdateStatus();
        }
    }

    int GetNextNoteOrder()
    {
        int order = 1;
        foreach (NoteItem note in CurrentDay.Notes)
        {
            order++;
        }

        return order;
    }

    int GetNextTaskOrder()
    {
        return CurrentDay.Tasks.Count + 1;
    }

    async Task UpdateTaskOrderAsync(IEnumerable<TaskItem>? additionallyChanged = null)
    {
        List<TaskItem> changedTasks = new();
        HashSet<string> changedIds = new(StringComparer.Ordinal);

        if (additionallyChanged != null)
        {
            foreach (TaskItem task in additionallyChanged)
            {
                if (task == null)
                {
                    continue;
                }

                if (changedIds.Add(task.Id))
                {
                    changedTasks.Add(task);
                }
            }
        }

        var orderByPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (TaskItem task in CurrentDay.Tasks)
        {
            string priorityKey = task.Priority ?? string.Empty;
            orderByPriority.TryGetValue(priorityKey, out int current);
            int nextOrder = current + 1;
            orderByPriority[priorityKey] = nextOrder;

            if (task.Order != nextOrder)
            {
                task.Order = nextOrder;
                if (changedIds.Add(task.Id))
                {
                    changedTasks.Add(task);
                }
            }
        }

        await _repo.UpdateTasksAsync(changedTasks);
    }

    static int GetPriorityRank(string? priority)
    {
        return priority?.ToUpperInvariant() switch
        {
            "A" => 0,
            "B" => 1,
            "C" => 2,
            _ => 3
        };
    }

    void SortTasksInMemory()
    {
        if (CurrentDay?.Tasks == null || CurrentDay.Tasks.Count < 2)
        {
            return;
        }

        var sorted = CurrentDay.Tasks
            .OrderBy(task => GetPriorityRank(task.Priority))
            .ThenBy(task => task.Order)
            .ThenBy(task => task.Id)
            .ToList();

        CurrentDay.Tasks.Clear();
        foreach (TaskItem task in sorted)
        {
            CurrentDay.Tasks.Add(task);
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
