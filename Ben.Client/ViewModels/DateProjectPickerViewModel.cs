using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ben.Models;
using Ben.Services;

namespace Ben.ViewModels;

public class DateProjectPickerViewModel : INotifyPropertyChanged
{
    private readonly DailyViewModel _dailyViewModel;
    private DateTime _selectedDate;
    private string _newProjectName = string.Empty;

    public DateProjectPickerViewModel(DailyViewModel dailyViewModel)
    {
        _dailyViewModel = dailyViewModel;
        _selectedDate = dailyViewModel.IsProjectPage ? DateTime.Today : dailyViewModel.CurrentDate;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectItem> Projects { get; } = new();

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set => SetField(ref _selectedDate, value.Date);
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set => SetField(ref _newProjectName, value);
    }

    public async Task InitializeAsync()
    {
        await RefreshProjectsAsync();
    }

    public async Task RefreshProjectsAsync()
    {
        List<ProjectItem> projects = await _dailyViewModel.GetProjectsAsync();
        Projects.Clear();
        foreach (ProjectItem project in projects)
        {
            Projects.Add(project);
        }
    }

    public Task NavigateToSelectedDateAsync()
    {
        return _dailyViewModel.NavigateToPageAsync(KeyConvention.ToDateKey(SelectedDate));
    }

    public Task NavigateToProjectAsync(ProjectItem project)
    {
        return _dailyViewModel.NavigateToPageAsync(KeyConvention.ToProjectKey(project.Name));
    }

    public async Task<(bool Success, string ErrorMessage)> CreateProjectAsync()
    {
        (bool success, string errorMessage, ProjectItem? project) = await _dailyViewModel.TryCreateProjectAsync(NewProjectName);
        if (!success)
        {
            return (false, errorMessage);
        }

        NewProjectName = string.Empty;
        await RefreshProjectsAsync();

        if (project != null)
        {
            await _dailyViewModel.NavigateToPageAsync(KeyConvention.ToProjectKey(project.Name));
        }

        return (true, string.Empty);
    }

    void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}