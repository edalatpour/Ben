using Bennie.Models;
using Bennie.Services;
using System.Collections;
using System.Linq;

namespace Bennie.Views;

public partial class DateProjectSelectorView : ContentView
{
    public static readonly BindableProperty ProjectsProperty = BindableProperty.Create(
        nameof(Projects),
        typeof(IEnumerable<ProjectItem>),
        typeof(DateProjectSelectorView),
        defaultValue: null,
        propertyChanged: OnProjectsChanged);

    public static readonly BindableProperty SelectedKeyProperty = BindableProperty.Create(
        nameof(SelectedKey),
        typeof(string),
        typeof(DateProjectSelectorView),
        defaultValue: null,
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnSelectedKeyChanged);

    public static readonly BindableProperty SelectionIndicatorTextProperty = BindableProperty.Create(
        nameof(SelectionIndicatorText),
        typeof(string),
        typeof(DateProjectSelectorView),
        defaultValue: "Selected: None");

    bool _isUpdating;

    public DateProjectSelectorView()
    {
        InitializeComponent();
        SelectorDatePicker.Date = DateTime.Today;
    }

    public IEnumerable<ProjectItem>? Projects
    {
        get => (IEnumerable<ProjectItem>?)GetValue(ProjectsProperty);
        set => SetValue(ProjectsProperty, value);
    }

    public string? SelectedKey
    {
        get => (string?)GetValue(SelectedKeyProperty);
        set => SetValue(SelectedKeyProperty, value);
    }

    public string SelectionIndicatorText
    {
        get => (string)GetValue(SelectionIndicatorTextProperty);
        private set => SetValue(SelectionIndicatorTextProperty, value);
    }

    public ProjectItem? SelectedProject => ProjectsCollectionView.SelectedItem as ProjectItem;

    public DateTime SelectedDate => SelectorDatePicker.Date ?? DateTime.Today;

    static void OnProjectsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not DateProjectSelectorView view)
        {
            return;
        }

        view.ProjectsCollectionView.ItemsSource = newValue as IEnumerable;
        view.ApplySelectedKeyToUi();
    }

    static void OnSelectedKeyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not DateProjectSelectorView view)
        {
            return;
        }

        view.ApplySelectedKeyToUi();
    }

    void OnDateSelected(object sender, DateChangedEventArgs e)
    {
        if (_isUpdating || !e.NewDate.HasValue)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            ProjectsCollectionView.SelectedItem = null;
            SelectedKey = KeyConvention.ToDateKey(e.NewDate.Value.Date);
            SelectionIndicatorText = $"Selected: Date ({e.NewDate.Value:D})";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    void OnProjectSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            if (e.CurrentSelection.FirstOrDefault() is ProjectItem project)
            {
                SelectedKey = KeyConvention.ToProjectKey(project.Id);
                SelectionIndicatorText = $"Selected: Project ({project.Name})";
            }
            else if (KeyConvention.TryParseDateKey(SelectedKey, out DateTime date))
            {
                SelectedKey = KeyConvention.ToDateKey(date);
                SelectionIndicatorText = $"Selected: Date ({date:D})";
            }
            else
            {
                DateTime selectedDate = SelectorDatePicker.Date ?? DateTime.Today;
                SelectedKey = KeyConvention.ToDateKey(selectedDate.Date);
                SelectionIndicatorText = $"Selected: Date ({selectedDate:D})";
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    void ApplySelectedKeyToUi()
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            if (KeyConvention.TryParseDateKey(SelectedKey, out DateTime date))
            {
                SelectorDatePicker.Date = date.Date;
                ProjectsCollectionView.SelectedItem = null;
                SelectionIndicatorText = $"Selected: Date ({date:D})";
                return;
            }

            if (KeyConvention.TryGetProjectId(SelectedKey, out string projectId))
            {
                ProjectItem? selectedProject = (ProjectsCollectionView.ItemsSource as IEnumerable<ProjectItem>)?
                    .FirstOrDefault(project => string.Equals(project.Id, projectId, StringComparison.Ordinal));

                ProjectsCollectionView.SelectedItem = selectedProject;
                SelectionIndicatorText = selectedProject == null
                    ? "Selected: Project"
                    : $"Selected: Project ({selectedProject.Name})";
                return;
            }

            SelectionIndicatorText = "Selected: None";
        }
        finally
        {
            _isUpdating = false;
        }
    }
}
