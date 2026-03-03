namespace Ben.Views;

using Ben.Models;
using Ben.ViewModels;

public partial class TaskPageView : ContentView
{
    public TaskPageView()
    {
        InitializeComponent();
    }

    async void OnTaskTapped(object sender, EventArgs e)
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

        await page.Navigation.PushModalAsync(new TaskDetailsPage(viewModel, task));
    }

    async void OnAddTaskTapped(object sender, EventArgs e)
    {
        await OpenNewTaskDetailsAsync();
    }

    async void OnEmptyAreaTapped(object sender, EventArgs e)
    {
        await OpenNewTaskDetailsAsync();
    }

    async Task OpenNewTaskDetailsAsync()
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        var page = Application.Current?.MainPage;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new TaskDetailsPage(viewModel));
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
}