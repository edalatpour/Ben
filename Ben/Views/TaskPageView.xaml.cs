namespace Ben.Views;

using Ben.Models;
using Ben.ViewModels;
using System.Linq;

public partial class TaskPageView : ContentView
{
    // DailyData _data;
    // TaskItemDatabase _db;

    public TaskPageView()
    {
        InitializeComponent();
    }

    async void OnNewTaskCompleted(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        string text = NormalizeInput(entry.Text);
        if (string.IsNullOrEmpty(text))
        {
            entry.Text = string.Empty;
            return;
        }

        entry.Text = string.Empty;
        await viewModel.AddTaskAsync(text);
        Dispatcher.Dispatch(() =>
        {
            if (TaskList != null && !IsElementVisibleIn(entry, TaskList))
            {
                TaskList.ScrollTo(entry.BindingContext, position: ScrollToPosition.MakeVisible, animate: true);
            }
            entry.Focus();
        });
    }

    static string NormalizeInput(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Trim non-breaking/zero-width spaces to avoid blank tasks.
        return text
            .Replace("\u00A0", " ")
            .Replace("\u200B", " ")
            .Replace("\uFEFF", " ")
            .Trim();
    }

    static bool IsElementVisibleIn(VisualElement element, VisualElement container)
    {
        if (element == null || container == null)
        {
            return false;
        }

        if (element.Height <= 0 || container.Height <= 0)
        {
            return false;
        }

        if (!TryGetRelativeY(element, container, out double relativeY))
        {
            return false;
        }

        double top = relativeY;
        double bottom = relativeY + element.Height;
        return top >= 0 && bottom <= container.Height;
    }

    static bool TryGetRelativeY(VisualElement element, VisualElement container, out double relativeY)
    {
        relativeY = 0;
        VisualElement current = element;

        while (current != null && current != container)
        {
            relativeY += current.Y + current.TranslationY;
            current = current.Parent as VisualElement;
        }

        return current == container;
    }

    async void OnEditTaskCompleted(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        if (entry.BindingContext is not TaskItem task || task.IsPlaceholder)
        {
            return;
        }

        task.IsEditing = false;

        if (string.IsNullOrWhiteSpace(task.Title))
        {
            task.Title = task.EditSnapshot ?? task.Title;
            task.EditSnapshot = null;
            return;
        }

        task.EditSnapshot = null;
        await viewModel.UpdateTaskAsync(task);
    }

    void OnTaskTitleTapped(object sender, EventArgs e)
    {
        if (sender is not Label label)
        {
            return;
        }

        if (label.BindingContext is not TaskItem task || task.IsPlaceholder)
        {
            return;
        }

        task.EditSnapshot = task.Title;
        task.IsEditing = true;

        if (label.Parent is Grid grid)
        {
            Entry editEntry = grid.Children
                .OfType<Entry>()
                .FirstOrDefault(entry => entry.StyleClass?.Contains("TaskTitleEditEntry") == true);
            editEntry?.Focus();
        }
    }

    async void OnTaskStatusTapped(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not BindableObject bindable)
        {
            return;
        }

        if (bindable.BindingContext is not TaskItem task || task.IsPlaceholder || task.IsPriorityBucket)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page == null)
        {
            return;
        }

        string selection = await page.DisplayActionSheetAsync("Status:", "Cancel", null, FlowDirection.LeftToRight, "(Not Started)", "● (In Progress)", "✅ (Completed)", "➡️ (Forwarded)", "❌ (Deleted)");
        // Debug.WriteLine("Action: " + action);
        // 

        // StatusEnum status = task.Status;
        String status = task.Status;

        switch (selection)
        {
            // case "(Not Started)":
            //     status = StatusEnum.NotStarted;
            //     break;
            // case "● (In Progress)":
            //     status = StatusEnum.InProgress;
            //     break;
            // case "✅ (Completed)":
            //     status = StatusEnum.Completed;
            //     break;
            // case "➡️ (Forwarded)":
            //     status = StatusEnum.Forwarded;
            //     break;
            // case "❌ (Deleted)":
            //     status = StatusEnum.Deleted;
            //     break;

            case "(Not Started)":
                status = "NotStarted";
                break;
            case "● (In Progress)":
                status = "InProgress";
                break;
            case "✅ (Completed)":
                status = "Completed";
                break;
            case "➡️ (Forwarded)":
                status = "Forwarded";
                break;
            case "❌ (Deleted)":
                status = "Deleted";
                break;
        }

        if (status != task.Status)
        {
            task.Status = status;
            await viewModel.UpdateTaskAsync(task);
        }

    }

    // static StatusEnum GetNextStatus(string status)
    // {
    //     if (string.IsNullOrWhiteSpace(status))
    //     {
    //         return StatusEnum.InProgress;
    //     }

    //     return status switch
    //     {
    //         StatusEnum.InProgress => StatusEnum.Completed,
    //         StatusEnum.Completed => For,
    //         "F" => "D",
    //         "D" => " ",
    //         _ => "I"
    //     };
    // }

    async void OnEditTaskUnfocused(object sender, FocusEventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (sender is not Entry entry)
        {
            return;
        }

        if (entry.BindingContext is not TaskItem task || task.IsPlaceholder)
        {
            return;
        }

        if (!task.IsEditing)
        {
            return;
        }

        string current = task.Title?.Trim() ?? string.Empty;
        string original = task.EditSnapshot ?? string.Empty;

        if (string.Equals(current, original, StringComparison.Ordinal))
        {
            task.IsEditing = false;
            task.EditSnapshot = null;
            return;
        }

        var page = Application.Current?.MainPage;
        if (page == null)
        {
            task.IsEditing = false;
            task.EditSnapshot = null;
            return;
        }

        string choice = await page.DisplayActionSheetAsync("Save changes?", "Continue editing", "Discard", "Save");
        if (choice == "Save")
        {
            task.IsEditing = false;
            task.EditSnapshot = null;
            await viewModel.UpdateTaskAsync(task);
            return;
        }

        if (choice == "Discard")
        {
            task.Title = task.EditSnapshot ?? task.Title;
            task.IsEditing = false;
            task.EditSnapshot = null;
            return;
        }

        task.IsEditing = true;
        entry.Focus();
    }

    void OnTaskDragStarting(object sender, DragStartingEventArgs e)
    {
        if (sender is not BindableObject bindable)
        {
            return;
        }

        if (bindable.BindingContext is not TaskItem task || task.IsPlaceholder || task.IsPriorityBucket)
        {
            e.Cancel = true;
            return;
        }

        e.Data.Properties["Task"] = task;
    }

    async void OnTaskDrop(object sender, DropEventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        if (!e.Data.Properties.TryGetValue("Task", out object dragged) || dragged is not TaskItem source)
        {
            return;
        }

        if (sender is not BindableObject bindable || bindable.BindingContext is not TaskItem target)
        {
            return;
        }

        if (ReferenceEquals(source, target))
        {
            return;
        }

        await viewModel.ReorderTaskAsync(source, target);
    }

    // public void Load(DailyData data)
    // {
    //     _data = data;
    //     BindingContext = _data;
    //     TaskList.ItemsSource = data.Tasks;
    // }    
    // public async Task Load(TaskItemDatabase db)
    // {
    //     _db = db;

    //     List<TaskItem> tasks = await _db.GetItemsAsync();
    //     BindingContext = tasks;
    //     // TaskList.ItemsSource = data.Tasks;
    // }
}