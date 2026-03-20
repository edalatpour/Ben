namespace Bennie.Views;

using Bennie.Models;
using Bennie.ViewModels;

public partial class TaskPageView : ContentView
{
    private readonly DailyViewModel _viewModel;

    public TaskPageView(DailyViewModel vm)
    {
        InitializeComponent();
        _viewModel = vm;
        BindingContext = _viewModel;
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

        if (bindable.BindingContext is not TaskItem task)
        {
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new TaskDetailsPage(viewModel, task));
    }

    async void OnAddTaskTapped(object sender, EventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new TaskDetailsPage(viewModel));
    }

    async void OnHeaderTapped(object sender, TappedEventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        await page.Navigation.PushModalAsync(new PageNavigationPage(viewModel));
    }

    void OnTaskDragStarting(object sender, DragStartingEventArgs e)
    {
        if (sender is not BindableObject bindable)
        {
            return;
        }

        if (bindable.BindingContext is not TaskItem task)
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