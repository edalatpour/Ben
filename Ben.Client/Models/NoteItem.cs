using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#nullable enable

namespace Bennie.Models;

public class NoteItem : INotifyPropertyChanged
{
    // [PrimaryKey, AutoIncrement]
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

    public string Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    string _id = Guid.NewGuid().ToString("N");
    DateTimeOffset? _updatedAt;
    string? _version;
    bool _deleted;
    string _key = string.Empty;
    string _text = string.Empty;
    int _order;
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
