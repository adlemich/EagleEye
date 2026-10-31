using System.Collections.ObjectModel;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// Merges a sorted list of items into an observable collection in place (US-008): rows of gone items are removed, rows of
/// kept items are updated and moved to their place, rows of new items are inserted there. Rows keep their instances, so
/// the views keep their state (ADR-010: merged by key). Keys must be unique. UI thread only.
/// </summary>
internal static class OrderedMerge
{
    /// <summary>Makes <paramref name="rows"/> follow <paramref name="items"/>.</summary>
    public static void Apply<TRow, TItem, TKey>(
        ObservableCollection<TRow> rows,
        IReadOnlyList<TItem> items,
        Func<TRow, TKey> rowKey,
        Func<TItem, TKey> itemKey,
        Func<TItem, TRow> create,
        Action<TRow, TItem> update)
    {
        var keys = items.Select(itemKey).ToHashSet();
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (!keys.Contains(rowKey(rows[index])))
            {
                rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < items.Count; index++)
        {
            var current = IndexOf(rows, index, itemKey(items[index]), rowKey);
            if (current < 0)
            {
                rows.Insert(index, create(items[index]));
                continue;
            }

            update(rows[current], items[index]);
            if (current != index)
            {
                rows.Move(current, index);
            }
        }
    }

    private static int IndexOf<TRow, TKey>(ObservableCollection<TRow> rows, int start, TKey key, Func<TRow, TKey> rowKey)
    {
        for (var index = start; index < rows.Count; index++)
        {
            if (EqualityComparer<TKey>.Default.Equals(rowKey(rows[index]), key))
            {
                return index;
            }
        }

        return -1;
    }
}
