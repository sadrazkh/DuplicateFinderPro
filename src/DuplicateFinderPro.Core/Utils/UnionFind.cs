namespace DuplicateFinderPro.Core.Utils;

/// <summary>
/// Disjoint-set structure used to cluster fuzzy/perceptual matches, where
/// pairwise similarity is transitive-ish (A~B, B~C ⇒ same group).
/// </summary>
public sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly int[] _rank;

    public UnionFind(int count)
    {
        _parent = new int[count];
        _rank = new int[count];
        for (var i = 0; i < count; i++) _parent[i] = i;
    }

    public int Find(int x)
    {
        while (_parent[x] != x)
        {
            _parent[x] = _parent[_parent[x]]; // path halving
            x = _parent[x];
        }
        return x;
    }

    public void Union(int a, int b)
    {
        var ra = Find(a);
        var rb = Find(b);
        if (ra == rb) return;

        if (_rank[ra] < _rank[rb]) (ra, rb) = (rb, ra);
        _parent[rb] = ra;
        if (_rank[ra] == _rank[rb]) _rank[ra]++;
    }

    /// <summary>Returns the members of each cluster keyed by representative root.</summary>
    public IEnumerable<List<int>> Clusters()
    {
        var map = new Dictionary<int, List<int>>();
        for (var i = 0; i < _parent.Length; i++)
        {
            var root = Find(i);
            if (!map.TryGetValue(root, out var list))
            {
                list = new List<int>();
                map[root] = list;
            }
            list.Add(i);
        }
        return map.Values;
    }
}
