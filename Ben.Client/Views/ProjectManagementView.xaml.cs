namespace Ben.Views;

public partial class ProjectManagementView : ContentView
{
    public static readonly BindableProperty ProjectNameProperty = BindableProperty.Create(
        nameof(ProjectName),
        typeof(string),
        typeof(ProjectManagementView),
        defaultValue: string.Empty,
        defaultBindingMode: BindingMode.TwoWay);

    public event EventHandler? AddRequested;
    public event EventHandler? EditRequested;

    public ProjectManagementView()
    {
        InitializeComponent();
    }

    public string ProjectName
    {
        get => (string)(GetValue(ProjectNameProperty) ?? string.Empty);
        set => SetValue(ProjectNameProperty, value);
    }

    void OnAddClicked(object sender, EventArgs e)
    {
        AddRequested?.Invoke(this, EventArgs.Empty);
    }

    void OnEditClicked(object sender, EventArgs e)
    {
        EditRequested?.Invoke(this, EventArgs.Empty);
    }
}
