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
using Ben.Views;

#nullable enable

namespace Ben.ViewModels;

public class DailyViewModel : INotifyPropertyChanged
{
    private const int MaxProjectNameLength = 128;

    public event PropertyChangedEventHandler? PropertyChanged;

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

        CurrentDate = DateTime.Today;
        _ = LoadDay(CurrentDate);

        // Subscribe to events
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;
        _syncService.SyncStarted += OnSyncStarted;
        _syncService.SyncCompleted += OnSyncCompleted;

        // Initial update
        _ = UpdateStatus();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        _ = UpdateStatus();
    }

    private void OnAuthenticationStateChanged(object? sender, EventArgs e)
    {
        _ = UpdateStatus();
    }

    private void OnSyncStarted(object? sender, EventArgs e)
    {
        _isSyncing = true;
        _ = UpdateStatus();
    }

    private void OnSyncCompleted(object? sender, EventArgs e)
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

    DailyData _currentDay = new() { Key = KeyConvention.ToDateKey(DateTime.Today), Date = DateTime.Today };
    public DailyData CurrentDay
    {
        get => _currentDay;
        set
        {
            _currentDay = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProjectPage));
            OnPropertyChanged(nameof(HeaderPrimaryText));
            OnPropertyChanged(nameof(HeaderSecondaryText));
            OnPropertyChanged(nameof(HeaderTertiaryText));
            OnPropertyChanged(nameof(ShowDateHeaderDetails));
        }
    }

    public bool IsProjectPage => KeyConvention.IsProjectKey(CurrentDay?.Key);

    public string HeaderPrimaryText
    {
        get
        {
            if (CurrentDay == null)
            {
                return string.Empty;
            }

            if (KeyConvention.TryGetProjectName(CurrentDay.Key, out string projectName))
            {
                return projectName;
            }

            return CurrentDate.ToString("dd");
        }
    }

    public string HeaderSecondaryText => IsProjectPage ? string.Empty : CurrentDate.ToString("dddd");

    public string HeaderTertiaryText => IsProjectPage ? string.Empty : CurrentDate.ToString("MMMM yyyy");

    public bool ShowDateHeaderDetails => !IsProjectPage;

    DateTime _currentDate;
    public DateTime CurrentDate
    {
        get => _currentDate;
        private set
        {
            DateTime normalized = value.Date;
            if (_currentDate == normalized)
            {
                return;
            }

            _currentDate = normalized;
            OnPropertyChanged();
        }
    }

    int _subPage = 0; // 0 = tasks, 1 = notes
    public int SubPage
    {
        get => _subPage;
        set { _subPage = value; OnPropertyChanged(); }
    }

    public async Task LoadDay(DateTime key)
    {
        await LoadPageAsync(KeyConvention.ToDateKey(key));
    }

    public async Task LoadPageAsync(string key)
    {
        if (KeyConvention.TryParseDateKey(key, out DateTime date))
        {
            CurrentDate = date;
        }

        DailyData day = await _repo.LoadPageAsync(key);
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
            Priority = "A",
            Order = GetSuggestedTaskOrder(CurrentDay.Key, "A"),
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
            task.Order = GetSuggestedTaskOrder(task.Key, task.Priority, task);
        }

        await _repo.AddTaskAsync(task);
        CurrentDay.Tasks.Add(task);
        await ApplyTaskPlacementAsync(task, task.Priority, task.Order);
        await UpdateStatus();
    }

    public Task<string?> GetTaskKeyByIdAsync(string taskId)
    {
        return _repo.GetTaskKeyByIdAsync(taskId);
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

    public async Task CreateForwardedTaskAsync(TaskItem originalTask, string destinationKey)
    {
        if (originalTask == null || string.IsNullOrWhiteSpace(destinationKey))
        {
            return;
        }

        if (string.Equals(originalTask.Key, destinationKey, StringComparison.Ordinal))
        {
            return;
        }

        var forwardedTask = new TaskItem
        {
            Title = originalTask.Title,
            Key = destinationKey,
            Status = "NotStarted",
            Priority = "A",
            Order = 1,
            ParentTaskId = originalTask.Id,
            OriginalTaskId = originalTask.OriginalTaskId ?? originalTask.Id
        };

        await _repo.AddTaskAsync(forwardedTask);
        await UpdateStatus();
    }

    public Task<List<ProjectItem>> GetProjectsAsync()
    {
        return _repo.GetProjectsAsync();
    }

    public async Task<(bool Success, string ErrorMessage, ProjectItem? Project)> TryCreateProjectAsync(string projectName)
    {
        string displayName = KeyConvention.NormalizeProjectDisplayName(projectName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (false, "Please enter a project name.", null);
        }

        if (displayName.Length > MaxProjectNameLength)
        {
            return (false, $"Project name must be {MaxProjectNameLength} characters or fewer.", null);
        }

        string normalizedName = KeyConvention.NormalizeProjectName(displayName);
        if (await _repo.ProjectExistsAsync(normalizedName))
        {
            return (false, "A project with that name already exists.", null);
        }

        var project = new ProjectItem
        {
            Name = displayName,
            NormalizedName = normalizedName
        };

        try
        {
            await _repo.AddProjectAsync(project);
            return (true, string.Empty, project);
        }
        catch (DbUpdateException)
        {
            return (false, "A project with that name already exists.", null);
        }
    }

    public async Task<(bool Success, string ErrorMessage)> TryRenameProjectAsync(ProjectItem project, string newProjectName)
    {
        if (project == null)
        {
            return (false, "Please select a project to edit.");
        }

        string displayName = KeyConvention.NormalizeProjectDisplayName(newProjectName);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return (false, "Please enter a project name.");
        }

        if (displayName.Length > MaxProjectNameLength)
        {
            return (false, $"Project name must be {MaxProjectNameLength} characters or fewer.");
        }

        string normalizedName = KeyConvention.NormalizeProjectName(displayName);
        if (await _repo.ProjectExistsAsync(normalizedName, project.Id))
        {
            return (false, "A project with that name already exists.");
        }

        project.Name = displayName;
        project.NormalizedName = normalizedName;

        try
        {
            await _repo.UpdateProjectAsync(project);
            return (true, string.Empty);
        }
        catch (DbUpdateException)
        {
            return (false, "A project with that name already exists.");
        }
    }

    public Task NavigateToPageAsync(string key)
    {
        return LoadPageAsync(key);
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
        if (SubPage == 0)
        {
            SubPage = 1;
            return;
        }

        SubPage = 0;
        await NavigatePageAsync(1);
    }

    public async Task GoBackwardAsync()
    {
        if (SubPage == 1)
        {
            SubPage = 0;
            return;
        }

        SubPage = 1;
        await NavigatePageAsync(-1);
    }

    public async Task NavigatePageAsync(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        string currentKey = CurrentDay?.Key ?? KeyConvention.ToDateKey(CurrentDate);
        if (KeyConvention.TryParseDateKey(currentKey, out DateTime currentDate))
        {
            await LoadDay(currentDate.AddDays(direction > 0 ? 1 : -1));
            return;
        }

        if (KeyConvention.TryGetProjectName(currentKey, out _))
        {
            List<string> projectKeys = await _repo.GetProjectKeysAsync();
            int currentIndex = projectKeys.FindIndex(key => string.Equals(key, currentKey, StringComparison.OrdinalIgnoreCase));

            if (projectKeys.Count > 0 && currentIndex >= 0)
            {
                int targetIndex = currentIndex + (direction > 0 ? 1 : -1);
                if (targetIndex >= 0 && targetIndex < projectKeys.Count)
                {
                    await LoadPageAsync(projectKeys[targetIndex]);
                    return;
                }

                if (direction > 0)
                {
                    string firstDate = await _repo.GetEarliestNonEmptyDateKeyAsync() ?? KeyConvention.ToDateKey(DateTime.Today);
                    await LoadPageAsync(firstDate);
                    return;
                }

                string latestDate = await _repo.GetLatestNonEmptyDateKeyAsync() ?? KeyConvention.ToDateKey(DateTime.Today);
                await LoadPageAsync(latestDate);
                return;
            }
        }

        await LoadDay(DateTime.Today);
    }

    void OnPropertyChanged([CallerMemberName] string? name = null)
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
                await LoadPageAsync(CurrentDay?.Key ?? KeyConvention.ToDateKey(CurrentDate));
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

    public (int Min, int Max) GetTaskOrderRange(string key, string priority, TaskItem? excludeTask)
    {
        if (CurrentDay?.Tasks == null)
        {
            return (1, 1);
        }

        string normalizedPriority = string.IsNullOrWhiteSpace(priority) ? "A" : priority;
        int samePriorityCount = CurrentDay.Tasks.Count(task =>
            !ReferenceEquals(task, excludeTask)
            && string.Equals(task.Key, key, StringComparison.Ordinal)
            && string.Equals(task.Priority, normalizedPriority, StringComparison.OrdinalIgnoreCase));

        return (1, Math.Max(1, samePriorityCount + 1));
    }

    public (int Min, int Max) GetTaskOrderRange(string key, string priority)
    {
        return GetTaskOrderRange(key, priority, excludeTask: null);
    }

    public int GetSuggestedTaskOrder(string key, string priority, TaskItem? excludeTask)
    {
        return GetTaskOrderRange(key, priority, excludeTask).Max;
    }

    public int GetSuggestedTaskOrder(string key, string priority)
    {
        return GetSuggestedTaskOrder(key, priority, excludeTask: null);
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
