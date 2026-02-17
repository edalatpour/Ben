using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Ben.Models;

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

    public DateTime Key
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

    [NotMapped]
    [JsonIgnore]
    public bool IsPlaceholder
    {
        get => _isPlaceholder;
        set => SetField(ref _isPlaceholder, value);
    }

    [NotMapped]
    [JsonIgnore]
    public bool IsEditing
    {
        get => _isEditing;
        set => SetField(ref _isEditing, value);
    }

    [NotMapped]
    [JsonIgnore]
    public string EditSnapshot
    {
        get => _editSnapshot;
        set => SetField(ref _editSnapshot, value);
    }

    string _id = Guid.NewGuid().ToString("N");
    DateTimeOffset? _updatedAt;
    string? _version;
    bool _deleted;
    DateTime _key;
    string _text;
    int _order;
    bool _isPlaceholder;
    bool _isEditing;
    string _editSnapshot;

    public event PropertyChangedEventHandler PropertyChanged;

    void SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
