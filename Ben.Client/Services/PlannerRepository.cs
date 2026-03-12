using System.Collections.ObjectModel;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Ben.Data;
using Ben.Models;

namespace Ben.Services;

public class PlannerRepository
{
    private readonly PlannerDbContext _db;
    private readonly DatasyncSyncService _syncService;

    public PlannerRepository(PlannerDbContext db, DatasyncSyncService syncService)
    {
        _db = db;
        _syncService = syncService;
    }

    public async Task<DailyData> LoadDayAsync(DateTime key)
    {
        var tasks = await _db.Tasks
            .Where(t => t.Key == key)
            .OrderBy(t => t.Priority == "A" ? 0
                : t.Priority == "B" ? 1
                : t.Priority == "C" ? 2
                : 3)
            .ThenBy(t => t.Order)
            .ThenBy(t => t.Id)
            .ToListAsync();

        var parentTaskIds = tasks
            .Where(task => !string.IsNullOrWhiteSpace(task.ParentTaskId))
            .Select(task => task.ParentTaskId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var originalTaskIds = tasks
            .Where(task => !string.IsNullOrWhiteSpace(task.OriginalTaskId))
            .Select(task => task.OriginalTaskId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var allReferencedIds = parentTaskIds
            .Union(originalTaskIds, StringComparer.Ordinal)
            .ToList();

        Dictionary<string, DateTime> keyById = allReferencedIds.Count == 0
            ? new Dictionary<string, DateTime>(StringComparer.Ordinal)
            : await _db.Tasks
                .Where(task => allReferencedIds.Contains(task.Id))
                .Select(task => new { task.Id, task.Key })
                .ToDictionaryAsync(task => task.Id, task => task.Key, StringComparer.Ordinal);

        foreach (var task in tasks)
        {
            // Task list shows the original task's date
            if (!string.IsNullOrWhiteSpace(task.OriginalTaskId)
                && keyById.TryGetValue(task.OriginalTaskId, out DateTime originalKey))
            {
                task.ForwardedFromDate = $"({originalKey:M/d})";
            }
            else if (!string.IsNullOrWhiteSpace(task.ParentTaskId)
                && keyById.TryGetValue(task.ParentTaskId, out DateTime parentKeyFallback))
            {
                task.ForwardedFromDate = $"({parentKeyFallback:M/d})";
            }
            else
            {
                task.ForwardedFromDate = null;
            }

            // Store parent task date separately for task details
            if (!string.IsNullOrWhiteSpace(task.ParentTaskId)
                && keyById.TryGetValue(task.ParentTaskId, out DateTime parentKey))
            {
                task.ParentTaskDate = $"{parentKey:M/d}";
            }
            else
            {
                task.ParentTaskDate = null;
            }
        }

        var notes = await _db.Notes
            .Where(n => n.Key == key)
            .OrderBy(n => n.Order)
            .ThenBy(n => n.Id)
            .ToListAsync();

        return new DailyData
        {
            Key = key,
            Tasks = new ObservableCollection<TaskItem>(tasks),
            Notes = new ObservableCollection<NoteItem>(notes)
        };
    }

    public async Task AddTaskAsync(TaskItem task)
    {
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public Task<DateTime?> GetTaskKeyByIdAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Task.FromResult<DateTime?>(null);
        }

        return _db.Tasks
            .Where(task => task.Id == taskId)
            .Select(task => (DateTime?)task.Key)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        _db.Tasks.Update(task);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task UpdateTasksAsync(IEnumerable<TaskItem> tasks)
    {
        List<TaskItem> uniqueTasks = tasks
            .Where(task => task != null)
            .GroupBy(task => task.Id)
            .Select(group => group.First())
            .ToList();

        if (uniqueTasks.Count == 0)
        {
            return;
        }

        _db.Tasks.UpdateRange(uniqueTasks);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task DeleteTaskAsync(TaskItem task)
    {
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task AddNoteAsync(NoteItem note)
    {
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task UpdateNoteAsync(NoteItem note)
    {
        _db.Notes.Update(note);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task DeleteNoteAsync(NoteItem note)
    {
        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task EnsureNoteOrderBackfillAsync()
    {
        await EnsureNoteOrderColumnAsync();

        if (!await _db.Notes.AnyAsync())
        {
            return;
        }

        bool hasNonZeroOrder = await _db.Notes.AnyAsync(note => note.Order != 0);
        if (hasNonZeroOrder)
        {
            return;
        }

        List<NoteItem> notes = await _db.Notes
            .OrderBy(note => note.Key)
            .ThenBy(note => note.Id)
            .ToListAsync();

        DateTime currentKey = default;
        int order = 0;

        foreach (NoteItem note in notes)
        {
            if (note.Key != currentKey)
            {
                currentKey = note.Key;
                order = 1;
            }

            note.Order = order++;
        }

        await _db.SaveChangesAsync();
    }

    async Task EnsureNoteOrderColumnAsync()
    {
        var connection = _db.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            bool hasOrder = false;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info('Notes');";
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string name = reader.GetString(reader.GetOrdinal("name"));
                    if (string.Equals(name, "Order", StringComparison.OrdinalIgnoreCase))
                    {
                        hasOrder = true;
                        break;
                    }
                }
            }

            if (!hasOrder)
            {
                using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Notes ADD COLUMN [Order] INTEGER NOT NULL DEFAULT 0;";
                await alterCommand.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
