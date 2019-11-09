using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ASI.Barista.Plugins.Imaging.Similarity
{
    public class BKTree<T>
    {
        private Node<T> _root;
        private readonly Func<T, byte[]> _keySelector;

        public BKTree()
        {
            if (typeof(T) == typeof(string)
                || typeof(T) == typeof(decimal)
                || typeof(T) == typeof(DateTime)
                || typeof(T) == typeof(DateTimeOffset)
                || typeof(T) == typeof(Guid)
                || typeof(T) == typeof(Uri)
                || typeof(T) == typeof(Version)
                || typeof(T) == typeof(TimeSpan)
                || typeof(T).IsEnum
                || typeof(T).IsPrimitive)
            {
                _keySelector = s => Encoding.UTF8.GetBytes(s.ToString());
            }

            if (typeof(T) == typeof(byte[]))
            {
                _keySelector = s => (byte[])(object)s;
            }
        }

        public BKTree(Func<T, byte[]> keySelector)
        {
            _keySelector = keySelector;
        }

        public void Add(T value)
        {
            if (_keySelector == null)
            {
                throw new ArgumentException("Cannot compute key for type " + typeof(T).FullName);
            }

            Add(_keySelector(value), value);
        }

        public void Add(byte[] key, T value)
        {
            if (_root == null)
            {
                _root = new Node<T>(key, value);
                return;
            }

            var curNode = _root;

            //var dist = LevenshteinDistance(curNode.Key, key);
            var dist = HammingDistance(curNode.Key, key);
            while (curNode.ContainsKey(dist))
            {
                if (dist == 0) return;

                curNode = curNode[dist];
                //dist = LevenshteinDistance(curNode.Key, key);
                dist = HammingDistance(curNode.Key, key);
            }

            curNode.AddChild(dist, key, value);
        }

        public List<byte[]> Search(byte[] term, int distance)
        {
            var result = new List<byte[]>();

            RecursiveSearch(_root, result, term, distance);

            return result;
        }

        private void RecursiveSearch(Node<T> node, List<byte[]> result, byte[] term, int distance)
        {
            //var curDist = LevenshteinDistance(node.Key, term);
            var curDist = HammingDistance(node.Key, term);
            var minDist = curDist - distance;
            var maxDist = curDist + distance;

            if (curDist <= distance)
            {
                result.Add(node.Key);
            }

            foreach (var key in node.Keys.Where(key => minDist <= key && key <= maxDist))
            {
                RecursiveSearch(node[key], result, term, distance);
            }
        }

        public static int LevenshteinDistance(byte[] first, byte[] second)
        {
            if (first.Length == 0) return second.Length;
            if (second.Length == 0) return first.Length;

            var lenFirst = first.Length;
            var lenSecond = second.Length;

            var d = new int[lenFirst + 1, lenSecond + 1];

            for (var i = 0; i <= lenFirst; i++)
                d[i, 0] = i;

            for (var i = 0; i <= lenSecond; i++)
                d[0, i] = i;

            for (var i = 1; i <= lenFirst; i++)
            {
                for (var j = 1; j <= lenSecond; j++)
                {
                    var match = (first[i - 1] == second[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + match);
                }
            }

            return d[lenFirst, lenSecond];
        }

        public static int HammingDistance(byte[] first, byte[] second)
        {
            if (first.Length == 0) return second.Length;
            if (second.Length == 0) return first.Length;

            var distance = new BitArray(first).Xor(new BitArray(second)).Cast<bool>().Count(x => x);

            return first.Zip(second, (c1, c2) => new { c1, c2 }).Count(m => m.c1 != m.c2);
        }
    }

    public class Node<T>
    {
        public byte[] Key { get; }

        public T Value { get; }

        public Dictionary<int, Node<T>> Children { get; private set; }

        public Node(byte[] key, T value)
        {
            Key = key;
            Value = value;
        }

        public Node<T> this[int key] => Children[key];

        public ICollection<int> Keys
        {
            get
            {
                if (Children == null) return new List<int>();
                return Children.Keys;
            }
        }

        public bool ContainsKey(int key)
        {
            return Children != null && Children.ContainsKey(key);
        }

        public void AddChild(int distance, byte[] key, T value)
        {
            if (Children == null)
            {
                Children = new Dictionary<int, Node<T>>();
            }
            Children[distance] = new Node<T>(key, value);
        }
    }
}
