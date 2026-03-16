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
        string parentDateValue = await _viewModel.GetPageDisplayAsync(parentKey);
        string parentDateText = !string.IsNullOrWhiteSpace(parentDateValue)
            ? $"Forwarded from {parentDateValue}."
            : "Forwarded.";

        if (!string.IsNullOrWhiteSpace(_task.OriginalTaskId)
            && _task.OriginalTaskId != _task.ParentTaskId)
        {
            string? originalKey = await _viewModel.GetTaskKeyByIdAsync(_task.OriginalTaskId);
            string originalDateValue = await _viewModel.GetPageDisplayAsync(originalKey);
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
        List<ProjectItem> forwardProjects = (await _viewModel.GetProjectsAsync())
            .Where(project => !string.Equals(KeyConvention.ToProjectKey(project.Id), _task.Key, StringComparison.Ordinal))
            .ToList();

        ForwardTargetSelector.Projects = forwardProjects;

        if (!string.IsNullOrWhiteSpace(_selectedForwardKey))
        {
            ForwardTargetSelector.SelectedKey = _selectedForwardKey;
            return;
        }

        DateTime sourceDate = KeyConvention.TryParseDateKey(_task.Key, out DateTime parsedDate)
            ? parsedDate
            : DateTime.Today;
        DateTime forwardDate = sourceDate < DateTime.Today.Date
            ? DateTime.Today
            : sourceDate.AddDays(1);

        ForwardTargetSelector.SelectedKey = KeyConvention.ToDateKey(forwardDate);
    }

    string? GetForwardDestinationKey()
    {
        _selectedForwardKey = ForwardTargetSelector.SelectedKey;
        return _selectedForwardKey;
    }
}
