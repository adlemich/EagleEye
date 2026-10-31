using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Rules;

/// <summary>
/// The writes of one row of a rule table on the Rules page: the application rules (US-006, the id is the app record)
/// and the app group rules (US-008 AC-16, the id is the group). Both return <c>true</c> when a snapshot confirming the
/// write was applied, and <c>false</c> on rejection, connection loss or timeout.
/// </summary>
public interface IRuleTableWrites
{
    /// <summary>Sets the hours or the minutes of the budget of a row (US-006 AC-7, AC-8).</summary>
    Task<bool> SetBudgetAsync(long id, AppBudgetPart part, int value);

    /// <summary>Ticks or unticks a weekday of a row (US-006 AC-7).</summary>
    Task<bool> SetDayAsync(long id, DayOfWeek day, bool isAllowed);
}
