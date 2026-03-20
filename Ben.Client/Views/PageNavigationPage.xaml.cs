using Bennie.Models;
using Bennie.ViewModels;
using Microsoft.Maui.Controls;

namespace Bennie.Views;

public partial class PageNavigationPage : ContentPage
{
    private readonly DateProjectPickerViewModel _viewModel;
    private bool _initialized;

    public PageNavigationPage(DailyViewModel dailyViewModel)
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
        PageSelector.SelectedKey = _viewModel.InitialKey;
    }

    async void OnAddProjectClicked(object sender, EventArgs e)
    {
        (bool success, string errorMessage) = await _viewModel.AddProjectAsync();
        if (!success)
        {
            await DisplayAlertAsync("Validation", errorMessage, "OK");
        }
    }

    async void OnEditProjectClicked(object sender, EventArgs e)
    {
        (bool success, string errorMessage) = await _viewModel.EditProjectAsync(PageSelector.SelectedProject);
        if (!success)
        {
            await DisplayAlertAsync("Validation", errorMessage, "OK");
        }
    }

    async void OnGoToPageClicked(object sender, EventArgs e)
    {
        await _viewModel.OpenSelectedPageAsync(PageSelector.SelectedKey);
        await Navigation.PopModalAsync();
    }

    async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}