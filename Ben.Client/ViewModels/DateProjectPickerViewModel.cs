using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Bennie.Models;
using Bennie.Services;

namespace Bennie.ViewModels;

public class DateProjectPickerViewModel : INotifyPropertyChanged
{
    private readonly DailyViewModel _dailyViewModel;
    private string _projectNameInput = string.Empty;

    public DateProjectPickerViewModel(DailyViewModel dailyViewModel)
    {
        _dailyViewModel = dailyViewModel;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProjectItem> Projects { get; } = new();

    public string InitialKey => _dailyViewModel.CurrentDay?.Key ?? KeyConvention.ToDateKey(_dailyViewModel.CurrentDate);

    public string ProjectNameInput
    {
        get => _projectNameInput;
        set => SetField(ref _projectNameInput, value);
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

    public async Task<(bool Success, string ErrorMessage)> AddProjectAsync()
    {
        (bool success, string errorMessage, ProjectItem? project) = await _dailyViewModel.TryCreateProjectAsync(ProjectNameInput);
        if (!success)
        {
            return (false, errorMessage);
        }

        ProjectNameInput = string.Empty;
        await RefreshProjectsAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Success, string ErrorMessage)> EditProjectAsync(ProjectItem? selectedProject)
    {
        if (selectedProject == null)
        {
            return (false, "Please select a project to edit.");
        }

        (bool success, string errorMessage) = await _dailyViewModel.TryRenameProjectAsync(selectedProject, ProjectNameInput);
        if (!success)
        {
            return (false, errorMessage);
        }

        await RefreshProjectsAsync();
        return (true, string.Empty);
    }

    public Task OpenSelectedPageAsync(string? selectedKey)
    {
        if (string.IsNullOrWhiteSpace(selectedKey))
        {
            selectedKey = InitialKey;
        }

        return _dailyViewModel.NavigateToPageAsync(selectedKey);
    }

    bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}