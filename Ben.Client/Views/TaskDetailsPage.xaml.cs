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
    private int _order;
    private string _selectedStatus = "NotStarted";

    public TaskDetailsPage(DailyViewModel viewModel, TaskItem task = null)
    {
        InitializeComponent();
        _viewModel = viewModel;

        foreach (string p in PriorityValues)
        {
            PriorityPicker.Items.Add(p);
        }

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

        int priorityIndex = Array.IndexOf(PriorityValues, _task.Priority);
        PriorityPicker.SelectedIndex = priorityIndex >= 0 ? priorityIndex : 0;

        _order = _task.Order > 0 ? _task.Order : 1;
        OrderLabel.Text = _order.ToString();
    }

    void OnStatusSelected(object sender, TappedEventArgs e)
    {
        _selectedStatus = e.Parameter?.ToString() ?? "NotStarted";
        UpdateStatusSelection();
    }

    void UpdateStatusSelection()
    {
        var accent = (Color)Application.Current.Resources["Accent"];
        var line = (Color)Application.Current.Resources["Line"];

        StatusBorderNotStarted.Stroke = _selectedStatus == "NotStarted" ? accent : line;
        StatusBorderInProgress.Stroke = _selectedStatus == "InProgress" ? accent : line;
        StatusBorderCompleted.Stroke = _selectedStatus == "Completed" ? accent : line;
        StatusBorderForwarded.Stroke = _selectedStatus == "Forwarded" ? accent : line;
        StatusBorderDeleted.Stroke = _selectedStatus == "Deleted" ? accent : line;
    }

    void OnOrderDown(object sender, EventArgs e)
    {
        if (_order > 1)
        {
            _order--;
            OrderLabel.Text = _order.ToString();
        }
    }

    void OnOrderUp(object sender, EventArgs e)
    {
        _order++;
        OrderLabel.Text = _order.ToString();
    }

    async void OnSaveClicked(object sender, EventArgs e)
    {
        string title = TitleEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title))
        {
            await DisplayAlertAsync("Validation", "Please enter a task title.", "OK");
            return;
        }

        _task.Title = title;
        _task.Status = _selectedStatus;
        _task.Priority = PriorityPicker.SelectedIndex >= 0 ? PriorityValues[PriorityPicker.SelectedIndex] : "A";
        _task.Order = _order;

        if (_isNewTask)
        {
            await _viewModel.AddTaskItemAsync(_task);
        }
        else
        {
            await _viewModel.UpdateTaskAsync(_task);
        }

        await Navigation.PopModalAsync();
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
