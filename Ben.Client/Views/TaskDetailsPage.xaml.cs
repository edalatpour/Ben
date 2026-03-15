namespace Ben.Views;

using Ben.Models;
using Ben.Services;
using Ben.ViewModels;

public partial class TaskDetailsPage : ContentPage
{
    static readonly string[] StatusValues = { "NotStarted", "InProgress", "Completed", "Forwarded", "Deleted" };
    static readonly string[] PriorityValues = { "A", "B", "C" };

    private readonly DailyViewModel _viewModel;
    private readonly TaskItem _task;
    private readonly bool _isNewTask;
    private int _minOrder = 1;
    private int _maxOrder = 1;
    private int _order;
    private int _priorityIndex;
    private string _selectedStatus = "NotStarted";
    private string? _selectedForwardKey;
    private bool _isForwardingToProject;
    private List<ProjectItem> _forwardProjects = new();

    public TaskDetailsPage(DailyViewModel viewModel, TaskItem? task = null)
    {
        InitializeComponent();
        _viewModel = viewModel;

        if (task == null)
        {
            _isNewTask = true;
            _task = new TaskItem
            {
                Key = viewModel.CurrentDay?.Key ?? KeyConvention.ToDateKey(DateTime.Today),
                Status = "NotStarted",
                Priority = "A",
                Order = 1
            };
        }
        else
        {
            _isNewTask = false;
            _task = task;
        }

        // Populate form fields
        TitleEntry.Text = _task.Title;
        _ = InitializeParentTaskDateAsync();

        _selectedStatus = Array.IndexOf(StatusValues, _task.Status) >= 0 ? _task.Status : "NotStarted";
        UpdateStatusSelection();

        _priorityIndex = Array.IndexOf(PriorityValues, _task.Priority);
        if (_priorityIndex < 0)
        {
            _priorityIndex = 0;
        }

        UpdatePriorityUi();

        _order = _task.Order > 0 ? _task.Order : 1;
        RefreshOrderForPriority();
        _ = InitializeForwardTargetsAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // DispatchDelayed ensures the modal transition is complete before
        // requesting focus, which is required on iOS for the keyboard to appear.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
        {
            TitleEntry.Focus();
            if (!_isNewTask && TitleEntry.Text?.Length > 0)
            {
                TitleEntry.CursorPosition = TitleEntry.Text.Length;
                TitleEntry.SelectionLength = 0;
            }
        });
    }

    async Task InitializeParentTaskDateAsync()
    {
        ParentTaskDateRow.IsVisible = false;
        ParentTaskDateLabel.Text = string.Empty;

        if (_isNewTask || string.IsNullOrWhiteSpace(_task.ParentTaskId))
        {
            return;
        }

        string? parentKey = await _viewModel.GetTaskKeyByIdAsync(_task.ParentTaskId);
        string parentDateValue = KeyConvention.ToShortPageDisplay(parentKey);
        string parentDateText = !string.IsNullOrWhiteSpace(parentDateValue)
            ? $"Forwarded from {parentDateValue}."
            : "Forwarded.";

        if (!string.IsNullOrWhiteSpace(_task.OriginalTaskId)
            && _task.OriginalTaskId != _task.ParentTaskId)
        {
            string? originalKey = await _viewModel.GetTaskKeyByIdAsync(_task.OriginalTaskId);
            string originalDateValue = KeyConvention.ToShortPageDisplay(originalKey);
            if (!string.IsNullOrWhiteSpace(originalDateValue))
            {
                parentDateText += $" Originally created on {originalDateValue}.";
            }
        }

        ParentTaskDateLabel.Text = parentDateText;
        ParentTaskDateRow.IsVisible = true;
    }

    void OnStatusSelected(object sender, TappedEventArgs e)
    {
        _selectedStatus = e.Parameter?.ToString() ?? "NotStarted";
        UpdateStatusSelection();
    }

    void UpdateStatusSelection()
    {
        if (Application.Current?.Resources == null)
        {
            return;
        }

        var accent = (Color)Application.Current.Resources["Accent"];
        var line = (Color)Application.Current.Resources["Line"];

        StatusBorderNotStarted.Stroke = _selectedStatus == "NotStarted" ? accent : line;
        StatusBorderInProgress.Stroke = _selectedStatus == "InProgress" ? accent : line;
        StatusBorderCompleted.Stroke = _selectedStatus == "Completed" ? accent : line;
        StatusBorderForwarded.Stroke = _selectedStatus == "Forwarded" ? accent : line;
        StatusBorderDeleted.Stroke = _selectedStatus == "Deleted" ? accent : line;

        bool showForwardedTarget = _selectedStatus == "Forwarded";
        ForwardingTargetHost.IsVisible = showForwardedTarget;

        if (!showForwardedTarget)
        {
            _selectedForwardKey = null;
        }
        else
        {
            EnsureForwardSelection();
            UpdateForwardTargetUi();
        }
    }

    void OnForwardedDateSelected(object sender, DateChangedEventArgs e)
    {
        if (!e.NewDate.HasValue)
        {
            _selectedForwardKey = null;
            return;
        }

        _selectedForwardKey = KeyConvention.ToDateKey(e.NewDate.Value);
    }

    void OnForwardedProjectChanged(object sender, EventArgs e)
    {
        if (ForwardedProjectPicker.SelectedIndex < 0 || ForwardedProjectPicker.SelectedIndex >= _forwardProjects.Count)
        {
            _selectedForwardKey = null;
            return;
        }

        _selectedForwardKey = KeyConvention.ToProjectKey(_forwardProjects[ForwardedProjectPicker.SelectedIndex].Name);
    }

    void OnForwardToDateClicked(object sender, EventArgs e)
    {
        _isForwardingToProject = false;
        EnsureForwardSelection();
        UpdateForwardTargetUi();
    }

    void OnForwardToProjectClicked(object sender, EventArgs e)
    {
        _isForwardingToProject = true;
        EnsureForwardSelection();
        UpdateForwardTargetUi();
    }

    void OnOrderDown(object sender, EventArgs e)
    {
        if (_order > _minOrder)
        {
            _order--;
            UpdateOrderUi();
        }
    }

    void OnOrderUp(object sender, EventArgs e)
    {
        if (_order < _maxOrder)
        {
            _order++;
            UpdateOrderUi();
        }
    }

    void OnPriorityUp(object sender, EventArgs e)
    {
        if (_priorityIndex > 0)
        {
            _priorityIndex--;
            UpdatePriorityUi();
            RefreshOrderForPriority();
        }
    }

    void OnPriorityDown(object sender, EventArgs e)
    {
        if (_priorityIndex < PriorityValues.Length - 1)
        {
            _priorityIndex++;
            UpdatePriorityUi();
            RefreshOrderForPriority();
        }
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        string title = TitleEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title))
        {
            await DisplayAlertAsync("Validation", "Please enter a task title.", "OK");
            return;
        }

        if (_selectedStatus == "Forwarded")
        {
            string? destinationKey = GetForwardDestinationKey();
            if (string.IsNullOrWhiteSpace(destinationKey))
            {
                await DisplayAlertAsync("Validation", "Please select a destination page.", "OK");
                return;
            }

            if (string.Equals(destinationKey, _task.Key, StringComparison.Ordinal))
            {
                await DisplayAlertAsync("Validation", "Please select a different page to forward this task to.", "OK");
                return;
            }
        }

        string selectedPriority = PriorityValues[_priorityIndex];
        _order = Math.Clamp(_order, _minOrder, _maxOrder);

        if (_isNewTask)
        {
            _task.Title = title;
            _task.Status = _selectedStatus;
            _task.Priority = selectedPriority;
            _task.Order = _order;
            await _viewModel.AddTaskItemAsync(_task);
        }
        else
        {
            await _viewModel.UpdateTaskFromDetailsAsync(_task, title, _selectedStatus, selectedPriority, _order);
        }

        if (_selectedStatus == "Forwarded")
        {
            string? destinationKey = GetForwardDestinationKey();
            if (!string.IsNullOrWhiteSpace(destinationKey))
            {
                await _viewModel.CreateForwardedTaskAsync(_task, destinationKey);
            }
        }

        await Navigation.PopModalAsync();
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    void RefreshOrderForPriority()
    {
        string selectedPriority = PriorityValues[_priorityIndex];
        string key = !string.IsNullOrWhiteSpace(_task.Key)
            ? _task.Key
            : _viewModel.CurrentDay?.Key ?? KeyConvention.ToDateKey(DateTime.Today);
        (int min, int max) = _viewModel.GetTaskOrderRange(key, selectedPriority, _isNewTask ? null : _task);

        _minOrder = min;
        _maxOrder = max;

        if (_isNewTask)
        {
            _order = _maxOrder;
        }
        else
        {
            _order = Math.Clamp(_order, _minOrder, _maxOrder);
        }

        UpdateOrderUi();
    }

    void UpdateOrderUi()
    {
        OrderLabel.Text = _order.ToString();
        OrderDownButton.IsEnabled = _order > _minOrder;
        OrderUpButton.IsEnabled = _order < _maxOrder;
    }

    void UpdatePriorityUi()
    {
        PriorityLabel.Text = PriorityValues[_priorityIndex];
        PriorityUpButton.IsEnabled = _priorityIndex > 0;
        PriorityDownButton.IsEnabled = _priorityIndex < PriorityValues.Length - 1;
    }

    async Task InitializeForwardTargetsAsync()
    {
        _forwardProjects = (await _viewModel.GetProjectsAsync())
            .Where(project => !string.Equals(KeyConvention.ToProjectKey(project.Name), _task.Key, StringComparison.Ordinal))
            .ToList();

        ForwardedProjectPicker.ItemsSource = _forwardProjects.Select(project => project.Name).ToList();
        EnsureForwardSelection();
        UpdateForwardTargetUi();
    }

    void EnsureForwardSelection()
    {
        if (_isForwardingToProject)
        {
            if (_forwardProjects.Count == 0)
            {
                _selectedForwardKey = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedForwardKey)
                || !KeyConvention.IsProjectKey(_selectedForwardKey)
                || !_forwardProjects.Any(project => string.Equals(KeyConvention.ToProjectKey(project.Name), _selectedForwardKey, StringComparison.Ordinal)))
            {
                _selectedForwardKey = KeyConvention.ToProjectKey(_forwardProjects[0].Name);
            }

            return;
        }

        if (!KeyConvention.TryParseDateKey(_selectedForwardKey, out DateTime _))
        {
            DateTime sourceDate = KeyConvention.TryParseDateKey(_task.Key, out DateTime parsedDate)
                ? parsedDate
                : DateTime.Today;
            DateTime forwardDate = sourceDate < DateTime.Today.Date
                ? DateTime.Today
                : sourceDate.AddDays(1);
            _selectedForwardKey = KeyConvention.ToDateKey(forwardDate);
        }
    }

    void UpdateForwardTargetUi()
    {
        if (Application.Current?.Resources == null)
        {
            return;
        }

        var accent = (Color)Application.Current.Resources["Accent"];
        var ink = (Color)Application.Current.Resources["Ink"];
        var line = (Color)Application.Current.Resources["Line"];
        var paper = (Color)Application.Current.Resources["WritingPaper"];

        ForwardToDateButton.BackgroundColor = _isForwardingToProject ? Colors.Transparent : accent;
        ForwardToDateButton.TextColor = _isForwardingToProject ? ink : paper;
        ForwardToDateButton.BorderColor = _isForwardingToProject ? line : accent;
        ForwardToDateButton.BorderWidth = _isForwardingToProject ? 1 : 0;

        ForwardToProjectButton.BackgroundColor = _isForwardingToProject ? accent : Colors.Transparent;
        ForwardToProjectButton.TextColor = _isForwardingToProject ? paper : ink;
        ForwardToProjectButton.BorderColor = _isForwardingToProject ? accent : line;
        ForwardToProjectButton.BorderWidth = _isForwardingToProject ? 0 : 1;

        ForwardedDatePicker.IsVisible = !_isForwardingToProject;
        ForwardedProjectPicker.IsVisible = _isForwardingToProject;

        if (!_isForwardingToProject && KeyConvention.TryParseDateKey(_selectedForwardKey, out DateTime forwardDate))
        {
            ForwardedDatePicker.Date = forwardDate;
        }

        if (_isForwardingToProject)
        {
            int selectedIndex = _forwardProjects.FindIndex(project => string.Equals(KeyConvention.ToProjectKey(project.Name), _selectedForwardKey, StringComparison.Ordinal));
            ForwardedProjectPicker.SelectedIndex = selectedIndex;
        }
    }

    string? GetForwardDestinationKey()
    {
        if (_isForwardingToProject)
        {
            if (ForwardedProjectPicker.SelectedIndex < 0 || ForwardedProjectPicker.SelectedIndex >= _forwardProjects.Count)
            {
                return null;
            }

            return KeyConvention.ToProjectKey(_forwardProjects[ForwardedProjectPicker.SelectedIndex].Name);
        }

        if (KeyConvention.TryParseDateKey(_selectedForwardKey, out DateTime forwardDate))
        {
            return KeyConvention.ToDateKey(forwardDate);
        }

        return null;
    }
}
