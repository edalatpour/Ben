using Ben.Models;
using Ben.Services;
using System.Collections;
using System.Linq;

namespace Ben.Views;

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
    DateTime? _selectedDate;

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

    public ProjectItem? SelectedProject => ProjectsPicker.SelectedItem as ProjectItem;

    public DateTime SelectedDate => _selectedDate ?? SelectorDatePicker.Date ?? DateTime.Today;

    static void OnProjectsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not DateProjectSelectorView view)
        {
            return;
        }

        IList? items = null;
        if (newValue is IList existingList)
        {
            items = existingList;
        }
        else if (newValue is IEnumerable enumerable)
        {
            items = enumerable.Cast<object>().ToList();
        }

        view.ProjectsPicker.ItemsSource = items;
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
            _selectedDate = e.NewDate.Value.Date;
            ProjectsPicker.SelectedItem = null;
            SelectedKey = KeyConvention.ToDateKey(e.NewDate.Value.Date);
            SelectionIndicatorText = $"Selected: Date ({e.NewDate.Value:D})";
        }
        finally
        {
            _isUpdating = false;
        }
    }

    void OnProjectSelectionChanged(object sender, EventArgs e)
    {
        if (_isUpdating)
        {
            return;
        }

        _isUpdating = true;
        try
        {
            if (ProjectsPicker.SelectedItem is ProjectItem project)
            {
                _selectedDate = null;
                SelectedKey = KeyConvention.ToProjectKey(project.Id);
                SelectionIndicatorText = $"Selected: Project ({project.Name})";
            }
            else if (KeyConvention.TryParseDateKey(SelectedKey, out DateTime date))
            {
                _selectedDate = date.Date;
                SelectedKey = KeyConvention.ToDateKey(date);
                SelectionIndicatorText = $"Selected: Date ({date:D})";
            }
            else
            {
                _selectedDate = null;
                SelectedKey = null;
                SelectionIndicatorText = "Selected: None";
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
                _selectedDate = date.Date;
                SelectorDatePicker.Date = date.Date;
                ProjectsPicker.SelectedItem = null;
                SelectionIndicatorText = $"Selected: Date ({date:D})";
                return;
            }

            if (KeyConvention.TryGetProjectId(SelectedKey, out string projectId))
            {
                ProjectItem? selectedProject = Projects?
                    .FirstOrDefault(project => string.Equals(project.Id, projectId, StringComparison.Ordinal));

                _selectedDate = null;
                ProjectsPicker.SelectedItem = selectedProject;
                SelectionIndicatorText = selectedProject == null
                    ? "Selected: Project"
                    : $"Selected: Project ({selectedProject.Name})";
                return;
            }

            _selectedDate = null;
            ProjectsPicker.SelectedItem = null;
            SelectionIndicatorText = "Selected: None";
        }
        finally
        {
            _isUpdating = false;
        }
    }
}
