using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#nullable enable

namespace Bennie.Models;

public class ProjectItem : INotifyPropertyChanged
{
    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public DateTimeOffset? UpdatedAt
    {
        get => _updatedAt;
        set => SetField(ref _updatedAt, value);
    }

    public string? Version
    {
        get => _version;
        set => SetField(ref _version, value);
    }

    public bool Deleted
    {
        get => _deleted;
        set => SetField(ref _deleted, value);
    }

    public string NormalizedName
    {
        get => _normalizedName;
        set => SetField(ref _normalizedName, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    string _id = Guid.NewGuid().ToString("N");
    DateTimeOffset? _updatedAt;
    string? _version;
    bool _deleted;
    string _normalizedName = string.Empty;
    string _name = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}