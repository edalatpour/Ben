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

    public Task<DailyData> LoadDayAsync(DateTime key)
    {
        return LoadPageAsync(KeyConvention.ToDateKey(key));
    }

    public async Task<DailyData> LoadPageAsync(string key)
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

        Dictionary<string, string> keyById = new(StringComparer.Ordinal);
        if (allReferencedIds.Count > 0)
        {
            var referencedKeys = await _db.Tasks
                .Where(task => allReferencedIds.Contains(task.Id))
                .Select(task => new { task.Id, task.Key })
                .ToListAsync();

            keyById = referencedKeys.ToDictionary(task => task.Id, task => task.Key, StringComparer.Ordinal);
        }

        foreach (var task in tasks)
        {
            // Task list shows the original task's date
            if (!string.IsNullOrWhiteSpace(task.OriginalTaskId)
                && keyById.TryGetValue(task.OriginalTaskId, out string? originalKey))
            {
                string originalDateText = KeyConvention.ToShortPageDisplay(originalKey);
                task.ForwardedFromDate = string.IsNullOrWhiteSpace(originalDateText) ? null : $"({originalDateText})";
            }
            else if (!string.IsNullOrWhiteSpace(task.ParentTaskId)
                && keyById.TryGetValue(task.ParentTaskId, out string? parentKeyFallback))
            {
                string parentDateText = KeyConvention.ToShortPageDisplay(parentKeyFallback);
                task.ForwardedFromDate = string.IsNullOrWhiteSpace(parentDateText) ? null : $"({parentDateText})";
            }
            else
            {
                task.ForwardedFromDate = null;
            }

            // Store parent task date separately for task details
            if (!string.IsNullOrWhiteSpace(task.ParentTaskId)
                && keyById.TryGetValue(task.ParentTaskId, out string? parentKey))
            {
                string parentDateText = KeyConvention.ToShortPageDisplay(parentKey);
                task.ParentTaskDate = string.IsNullOrWhiteSpace(parentDateText) ? null : parentDateText;
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
            Date = KeyConvention.TryParseDateKey(key, out DateTime date) ? date : DateTime.Today,
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

    public Task<string?> GetTaskKeyByIdAsync(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return Task.FromResult<string?>(null);
        }

        return _db.Tasks
            .Where(task => task.Id == taskId)
            .Select(task => task.Key)
            .FirstOrDefaultAsync();
    }

    public Task<List<string>> GetProjectKeysAsync()
    {
        return _db.Projects
            .Where(project => !project.Deleted)
            .OrderBy(project => project.Name)
            .Select(project => KeyConvention.ToProjectKey(project.Name))
            .ToListAsync();
    }

    public async Task<List<ProjectItem>> GetProjectsAsync()
    {
        return await _db.Projects
            .Where(project => !project.Deleted)
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .ToListAsync();
    }

    public Task<bool> ProjectExistsAsync(string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Task.FromResult(false);
        }

        return _db.Projects.AnyAsync(project => !project.Deleted && project.NormalizedName == normalizedName);
    }

    public async Task AddProjectAsync(ProjectItem project)
    {
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        _ = _syncService.TriggerSyncAsync();
    }

    public async Task<string?> GetEarliestNonEmptyDateKeyAsync()
    {
        List<string> taskDateKeys = await _db.Tasks
            .Where(task => task.Key.StartsWith(KeyConvention.DatePrefix))
            .Select(task => task.Key)
            .Distinct()
            .ToListAsync();

        List<string> noteDateKeys = await _db.Notes
            .Where(note => note.Key.StartsWith(KeyConvention.DatePrefix))
            .Select(note => note.Key)
            .Distinct()
            .ToListAsync();

        return taskDateKeys
            .Concat(noteDateKeys)
            .Where(KeyConvention.IsDateKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public async Task<string?> GetLatestNonEmptyDateKeyAsync()
    {
        List<string> taskDateKeys = await _db.Tasks
            .Where(task => task.Key.StartsWith(KeyConvention.DatePrefix))
            .Select(task => task.Key)
            .Distinct()
            .ToListAsync();

        List<string> noteDateKeys = await _db.Notes
            .Where(note => note.Key.StartsWith(KeyConvention.DatePrefix))
            .Select(note => note.Key)
            .Distinct()
            .ToListAsync();

        return taskDateKeys
            .Concat(noteDateKeys)
            .Where(KeyConvention.IsDateKey)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(key => key, StringComparer.Ordinal)
            .FirstOrDefault();
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

        string currentKey = string.Empty;
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
