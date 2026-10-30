using System;
using System.Collections.Generic;
using UnityEngine;

namespace SDGAdventure.SharedPlayer
{
    [Serializable]
    public sealed class PlayerTaskItem
    {
        public string id;
        public string title;
        [TextArea] public string description;
        public bool completed;

        public PlayerTaskItem(string id, string title, string description, bool completed = false)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.completed = completed;
        }

        public PlayerTaskItem Copy() => new PlayerTaskItem(id, title, description, completed);
    }

    // Each level can provide its own task data without depending on another level's scripts.
    public interface IPlayerTaskSource
    {
        IReadOnlyList<PlayerTaskItem> GetTasks();
    }

    public sealed class PlayerTaskJournal : MonoBehaviour
    {
        private readonly List<PlayerTaskItem> tasks = new List<PlayerTaskItem>();
        public IReadOnlyList<PlayerTaskItem> Tasks => tasks;
        public int CompletedCount { get; private set; }
        public int TotalCount => tasks.Count;
        public event Action Changed;

        public void Load(IPlayerTaskSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            SetTasks(source.GetTasks());
        }

        public void SetTasks(IEnumerable<PlayerTaskItem> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var replacement = new List<PlayerTaskItem>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayerTaskItem entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) ||
                    string.IsNullOrWhiteSpace(entry.title) || !ids.Add(entry.id))
                    throw new ArgumentException("Tasks need unique ids and titles.", nameof(entries));
                replacement.Add(entry.Copy());
            }
            tasks.Clear();
            tasks.AddRange(replacement);
            Recount();
            Changed?.Invoke();
        }

        public bool SetCompleted(string id, bool completed = true)
        {
            foreach (PlayerTaskItem task in tasks)
            {
                if (task.id != id) continue;
                if (task.completed == completed) return true;
                task.completed = completed;
                Recount();
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        private void Recount()
        {
            CompletedCount = 0;
            foreach (PlayerTaskItem task in tasks)
                if (task.completed) CompletedCount++;
        }
    }
}
