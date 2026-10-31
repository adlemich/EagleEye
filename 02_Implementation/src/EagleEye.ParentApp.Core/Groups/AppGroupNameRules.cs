using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;

namespace EagleEye.ParentApp.Core.Groups;

/// <summary>
/// The name check of the group dialog (US-008 AC-8, AC-9): the shared rules of <see cref="AppGroupNames"/> and no other
/// group with the same name ignoring case. The group being renamed is ignored, so its own name (also re-cased) is valid.
/// </summary>
public static class AppGroupNameRules
{
    /// <summary>The message to show for <paramref name="name"/>, or <c>null</c> when the name is valid.</summary>
    /// <param name="name">The typed name.</param>
    /// <param name="groups">The groups of the replica.</param>
    /// <param name="renamedGroupId">The group being renamed, or null for a new group.</param>
    public static string? Problem(string? name, IEnumerable<AppGroupDto> groups, long? renamedGroupId)
    {
        ArgumentNullException.ThrowIfNull(groups);
        if (!AppGroupNames.IsValid(name))
        {
            return AppTexts.GroupsInvalidName;
        }

        var key = AppGroupNames.Key(name);
        return groups.Any(g => g.GroupId != renamedGroupId && AppGroupNames.Key(g.Name) == key) ? AppTexts.GroupsDuplicateName : null;
    }
}
