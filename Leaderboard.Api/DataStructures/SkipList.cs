using System.Collections;

namespace Leaderboard.Api.DataStructures
{
    // Implementing a skip list is simpler than implementing a red-black tree,
    // and in this scene, the skip list will not degenerate to the worst-case.
    public sealed class SkipList<T> : IEnumerable<T> where T : IComparable<T>
    {
        private sealed class SkipListNode
        {
            public readonly T Value;
            public readonly SkipListNode?[] Forward;
            public readonly int[] Span;
            public SkipListNode? Backward;

            public SkipListNode(T value, int level)
            {
                Value = value;
                Forward = new SkipListNode[level];
                Span = new int[level];
                for (int i = 0; i < Span.Length; i++)
                {
                    Span[i] = 1;
                }
            }
        }

        public int Count { get; private set; }

        private const double Probability = 0.5;
        private const int MaxLevel = 32;

        private readonly SkipListNode _head = new SkipListNode(default!, MaxLevel);
        private readonly Random _rnd = new();
        private int _level = 1;

        public bool Add(T value)
        {
            var update = new SkipListNode[MaxLevel];
            var rank = new int[MaxLevel];
            var x = _head;

            for (int i = _level - 1; i >= 0; i--)
            {
                rank[i] = i == _level - 1 ? 0 : rank[i + 1];
                while (x.Forward[i] != null && x.Forward[i]!.Value.CompareTo(value) < 0)
                {
                    rank[i] += x.Span[i];
                    x = x.Forward[i]!;
                }
                update[i] = x;
            }

            int level = RandomLevel();
            if (level > _level)
            {
                for (int i = _level; i < level; i++)
                {
                    rank[i] = 0;
                    update[i] = _head;
                    _head.Span[i] = Count;
                }
                _level = level;
            }

            var node = new SkipListNode(value, level);
            for (int i = 0; i < level; i++)
            {
                node.Forward[i] = update[i].Forward[i];
                update[i].Forward[i] = node;

                node.Span[i] = update[i].Span[i] - (rank[0] - rank[i]);
                update[i].Span[i] = (rank[0] - rank[i]) + 1;
            }

            for (int i = level; i < _level; i++)
            {
                update[i].Span[i]++;
            }

            node.Backward = update[0] == _head ? null : update[0];
            if (node.Forward[0] != null)
            {
                node.Forward[0]!.Backward = node;
            }

            Count++;
            return true;
        }

        public bool Remove(T value)
        {
            var update = new SkipListNode[MaxLevel];
            var x = _head;

            for (int i = _level - 1; i >= 0; i--)
            {
                while (x.Forward[i] != null && x.Forward[i]!.Value.CompareTo(value) < 0)
                {
                    x = x.Forward[i]!;
                }
                update[i] = x;
            }

            x = x.Forward[0]!;
            if (x == null || x.Value.CompareTo(value) != 0)
            {
                return false;
            }

            RemoveNode(x, update);
            return true;
        }

        public IEnumerable<T> RangeByValue(T lowValue, T highValue)
        {
            var node = FindFirstGreaterOrEqual(lowValue);
            while (node != null && node.Value.CompareTo(highValue) <= 0)
            {
                yield return node.Value;
                node = node.Forward[0];
            }
        }

        public IEnumerable<T> RangeByRank(int lowRank, int highRank)
        {
            if (lowRank < 1 || highRank < lowRank || highRank > Count)
            {
                yield break;
            }

            var node = GetNodeByRank(lowRank - 1);
            int diff = highRank - lowRank + 1;

            while (node != null && diff-- > 0)
            {
                yield return node.Value;
                node = node.Forward[0];
            }
        }

        public int GetRankByValue(T value)
        {
            int rank = 0;
            var x = _head;

            for (int i = _level - 1; i >= 0; i--)
            {
                while (x.Forward[i] != null && x.Forward[i]!.Value.CompareTo(value) < 0)
                {
                    rank += x.Span[i];
                    x = x.Forward[i]!;
                }
            }

            x = x.Forward[0];
            if (x != null && x.Value.CompareTo(value) == 0)
            {
                return rank + 1;
            }

            return -1;
        }

        private SkipListNode? GetNodeByRank(int rank)
        {
            int tempRank = 0;
            var x = _head;

            for (int i = _level - 1; i >= 0; i--)
            {
                while (x.Forward[i] != null && tempRank + x.Span[i] <= rank)
                {
                    tempRank += x.Span[i];
                    x = x.Forward[i]!;
                }

                if (tempRank == rank)
                {
                    return x.Forward[0];
                }
            }

            return null;
        }

        private SkipListNode? FindFirstGreaterOrEqual(T value)
        {
            var x = _head;
            for (int i = _level - 1; i >= 0; i--)
            {
                while (x.Forward[i] != null && x.Forward[i]!.Value.CompareTo(value) < 0)
                {
                    x = x.Forward[i]!;
                }
            }
            return x.Forward[0];
        }

        private void RemoveNode(SkipListNode node, SkipListNode[] update)
        {
            for (int i = 0; i < _level; i++)
            {
                if (update[i].Forward[i] == node)
                {
                    update[i].Span[i] += node.Span[i] - 1;
                    update[i].Forward[i] = node.Forward[i];
                }
                else
                {
                    update[i].Span[i]--;
                }
            }

            if (node.Forward[0] != null)
            {
                node.Forward[0]!.Backward = node.Backward;
            }

            while (_level > 1 && _head.Forward[_level - 1] == null)
            {
                _level--;
            }

            Count--;
        }

        private int RandomLevel()
        {
            int lvl = 1;
            while (_rnd.NextDouble() < Probability && lvl < MaxLevel)
            {
                lvl++;
            }
            return lvl;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var current = _head.Forward[0];
            while (current != null)
            {
                yield return current.Value;
                current = current.Forward[0];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}