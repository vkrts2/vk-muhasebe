using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ErmayMuhasebe.Helpers;

public static class ObservableCollectionExtensions
{
    /// <summary>
    /// Synchronizes an ObservableCollection with a source sequence in-place.
    /// Preserves existing object instances, UI controls, context menus, flyouts, and selection.
    /// Avoids collection resets and unnecessary notifications when items are unchanged.
    /// </summary>
    public static void SyncWith<T, TKey>(
        this ObservableCollection<T> target,
        IEnumerable<T> source,
        Func<T, TKey> keySelector,
        Action<T, T>? updateAction = null) where TKey : notnull
    {
        if (target == null) return;
        var sourceList = (source ?? Enumerable.Empty<T>()).ToList();

        if (target.Count == 0)
        {
            foreach (var item in sourceList)
            {
                target.Add(item);
            }
            return;
        }

        if (sourceList.Count == 0)
        {
            target.Clear();
            return;
        }

        var sourceKeys = new HashSet<TKey>(sourceList.Select(keySelector));

        // 1. Remove deleted items (from end to start to maintain index validity)
        for (int i = target.Count - 1; i >= 0; i--)
        {
            var key = keySelector(target[i]);
            if (!sourceKeys.Contains(key))
            {
                target.RemoveAt(i);
            }
        }

        // Build dictionary of remaining target items (safely ignore duplicate keys if any exist)
        var targetDict = new Dictionary<TKey, T>();
        foreach (var item in target)
        {
            var key = keySelector(item);
            targetDict.TryAdd(key, item);
        }

        // 2. Insert or update in the source order
        for (int i = 0; i < sourceList.Count; i++)
        {
            var src = sourceList[i];
            var key = keySelector(src);

            if (targetDict.TryGetValue(key, out var existing))
            {
                // In-place update of properties
                updateAction?.Invoke(existing, src);

                int currentIndex = target.IndexOf(existing);
                if (currentIndex != i && currentIndex >= 0 && currentIndex < target.Count)
                {
                    target.Move(currentIndex, i);
                }
            }
            else
            {
                // New item inserted at correct position
                target.Insert(i, src);
                targetDict.TryAdd(key, src);
            }
        }
    }
}
