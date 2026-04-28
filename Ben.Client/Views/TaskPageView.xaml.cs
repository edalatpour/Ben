namespace Ben.Views;

using Ben.Models;
using Ben.Services;
using Ben.ViewModels;

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

    async void OnDateTapped(object sender, TappedEventArgs e)
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

    async void OnCatchUpClicked(object sender, TappedEventArgs e)
    {
        if (BindingContext is not DailyViewModel viewModel)
        {
            return;
        }

        string? currentKey = viewModel.CurrentDay?.Key;
        if (string.IsNullOrWhiteSpace(currentKey)
            || !KeyConvention.TryParseDateKey(currentKey, out DateTime destinationDate))
        {
            return;
        }

        string destinationDateKey = currentKey;
        var preview = await viewModel.BuildCatchUpForwardSqlPreviewAsync(destinationDateKey);

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page == null)
        {
            return;
        }

        if (preview.Count <= 0)
        {
            await page.DisplayAlertAsync("", $"No open tasks to pull forward.", "OK");
            return;
        }


        String word = "tasks";
        if (preview.Count == 1)
        {
            word = "task";
        }

        bool shouldForward = await page.DisplayAlertAsync(
            "",
            $"Pull {preview.Count} open {word} forward?",
            "Yes",
            "No");

        if (!shouldForward)
        {
            return;
        }

        var execution = await viewModel.ExecuteCatchUpAsync(destinationDateKey);
        // await page.DisplayAlertAsync(
        //     "",
        //     $"Forwarded {execution.CandidateCount} open task(s) to {destinationDate:yyyy-MM-dd}.",
        //     "OK");
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