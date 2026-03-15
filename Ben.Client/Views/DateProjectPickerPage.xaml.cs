using Ben.Models;
using Ben.ViewModels;

namespace Ben.Views;

public partial class DateProjectPickerPage : ContentPage
{
    private readonly DateProjectPickerViewModel _viewModel;
    private bool _initialized;

    public DateProjectPickerPage(DailyViewModel dailyViewModel)
    {
        InitializeComponent();
        _viewModel = new DateProjectPickerViewModel(dailyViewModel);
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    async void OnOpenDateClicked(object sender, EventArgs e)
    {
        await _viewModel.NavigateToSelectedDateAsync();
        await Navigation.PopModalAsync();
    }

    async void OnProjectSelected(object sender, EventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not ProjectItem project)
        {
            return;
        }

        await _viewModel.NavigateToProjectAsync(project);
        await Navigation.PopModalAsync();
    }

    async void OnCreateProjectClicked(object sender, EventArgs e)
    {
        (bool success, string errorMessage) = await _viewModel.CreateProjectAsync();
        if (!success)
        {
            await DisplayAlertAsync("Validation", errorMessage, "OK");
            return;
        }

        await Navigation.PopModalAsync();
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}