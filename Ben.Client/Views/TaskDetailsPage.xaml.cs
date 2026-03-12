namespace Ben.Views;

using Ben.Models;
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
    private DateTime? _selectedForwardedDate;

    public TaskDetailsPage(DailyViewModel viewModel, TaskItem? task = null)
    {
        InitializeComponent();
        _viewModel = viewModel;

        if (task == null)
        {
            _isNewTask = true;
            _task = new TaskItem
            {
                Key = viewModel.CurrentDay?.Key ?? DateTime.Today,
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

        bool showForwardedDate = _selectedStatus == "Forwarded";
        ForwardedDatePickerHost.IsVisible = showForwardedDate;

        if (!showForwardedDate)
        {
            _selectedForwardedDate = null;
        }
        else
        {
            if (!_selectedForwardedDate.HasValue)
            {
                DateTime taskKey = _task.Key != default ? _task.Key.Date : DateTime.Today;
                _selectedForwardedDate = taskKey < DateTime.Today.Date
                    ? DateTime.Today
                    : taskKey.AddDays(1);
            }

            ForwardedDatePicker.Date = _selectedForwardedDate.Value;
        }
    }

    void OnForwardedDateSelected(object sender, DateChangedEventArgs e)
    {
        _selectedForwardedDate = e.NewDate;
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
            DateTime forwardDate = _selectedForwardedDate.HasValue ? _selectedForwardedDate.Value : DateTime.Today;
            if (forwardDate.Date == _task.Key.Date)
            {
                await DisplayAlertAsync("Validation", "Please select a different date to forward this task to.", "OK");
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
            DateTime forwardDate = _selectedForwardedDate.HasValue ? _selectedForwardedDate.Value : DateTime.Today;
            await _viewModel.CreateForwardedTaskAsync(_task, forwardDate);
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
        DateTime key = _task.Key != default ? _task.Key : _viewModel.CurrentDay?.Key ?? DateTime.Today;
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
}
