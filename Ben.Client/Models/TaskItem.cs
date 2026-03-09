using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Ben.Models;

public enum StatusEnum
{
    NotStarted,
    InProgress,
    Forwarded,
    Completed,
    Deleted
}

public class TaskItem : INotifyPropertyChanged
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

    // public String Status
    // {
    //     get => _status;
    //     set => SetField(ref _status, value);
    // }

    public String Status
    {
        get => _status;
        // set => SetField(ref _status, value);
        set
        {
            if (SetField(ref _status, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusGlyph)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusIcon)));
            }
        }
    }

    [NotMapped]
    [JsonIgnore]
    public string StatusGlyph
    {
        get
        {
            return _status switch
            {
                "InProgress" => "⏺️",
                "Completed" => "✅",
                "Forwarded" => "➡️",
                "Deleted" => "❌",
                _ => string.Empty
            };
        }
    }

    [NotMapped]
    [JsonIgnore]
    public string StatusIcon
    {
        get
        {
            return _status switch
            {
                "InProgress" => "in_progress.webp",
                "Completed" => "completed.webp",
                "Forwarded" => "forwarded.webp",
                "Deleted" => "cancelled.webp",
                _ => string.Empty
            };
        }
    }

    public string Priority
    {
        get => _priority;
        set => SetField(ref _priority, value);
    }

    public int Order
    {
        get => _order;
        set => SetField(ref _order, value);
    }

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
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
    public bool IsAddPlaceholder
    {
        get => _isAddPlaceholder;
        set => SetField(ref _isAddPlaceholder, value);
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
    // string _status;
    String _status;
    string _priority;
    int _order;
    string _title;
    bool _isPlaceholder;
    bool _isAddPlaceholder;
    bool _isEditing;
    string _editSnapshot;

    public event PropertyChangedEventHandler PropertyChanged;

    bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
