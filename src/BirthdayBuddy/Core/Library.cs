namespace BirthdayBuddy.Core;

/// <summary>The live birthday library: in-memory data, saved on every change.</summary>
/// <param name="readOnly">Keep changes in memory only, e.g. when the file couldn't be opened and mustn't be overwritten.</param>
public sealed class Library(BirthdayStore store, AppData data, bool readOnly = false)
{
    public AppData Data { get; private set; } = data;

    /// <summary>Raised when people or settings change (not when reminder markers are recorded).</summary>
    public event EventHandler? Changed;

    public void Upsert(Person person) =>
        Commit(Data with { People = Data.People.Where(p => p.Id != person.Id).Append(person).ToList() }, notify: true);

    public void Remove(Guid id) =>
        Commit(Data with { People = Data.People.Where(p => p.Id != id).ToList() }, notify: true);

    public void UpdateSettings(Settings settings) =>
        Commit(Data with { Settings = settings }, notify: true);

    /// <summary>Saves sent-reminder markers. Doesn't raise Changed: a check must not trigger another check.</summary>
    public void RecordSent(IReadOnlyList<Person> people) =>
        Commit(Data with { People = people }, notify: false);

    void Commit(AppData next, bool notify)
    {
        Data = next;
        if (!readOnly) try
        {
            store.Save(next);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Could not save birthdays.json.", ex);
        }
        if (notify) Changed?.Invoke(this, EventArgs.Empty);
    }
}
