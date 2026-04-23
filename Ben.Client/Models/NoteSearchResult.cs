using System;

namespace Ben.Models;

public sealed class NoteSearchResult
{
    public string NoteId { get; init; } = string.Empty;

    public string DateKey { get; init; } = string.Empty;

    public DateTime Date { get; init; }

    public int Order { get; init; }

    public string Text { get; init; } = string.Empty;
}