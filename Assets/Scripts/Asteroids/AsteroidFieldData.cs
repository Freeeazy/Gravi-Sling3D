using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Asteroids/Asteroid Field Data")]
public class AsteroidFieldData : ScriptableObject
{
    [Header("Metadata")]
    public Vector3 fieldCenter = Vector3.zero;
    public Vector3 fieldSize = new Vector3(200f, 200f, 200f);
    public bool useFixedSeed = false;
    public int seed = 12345;

    [Header("Instances")]
    public int count;

    public Vector3[] positions;
    public Quaternion[] rotations;
    public float[] scales;              // uniform scale
    public int[] typeIds;               // asteroid family index
    public Vector3[] angularVelocityDeg; // degrees/sec per axis
    public float[] baseRadii;

    [Header("Spatial Index (Baked Grid)")]
    [Min(0.0001f)] public float cellSize = 20f;

    // Min corner of the field bounds used for cell coord computations.
    public Vector3 gridOrigin;

    // Sorted unique keys for occupied cells.
    public long[] cellKeys;

    // Start offsets into cellIndices for each cell key. Length = cellKeys.Length + 1.
    public int[] cellStarts;

    // Flattened asteroid indices grouped by cell.
    public int[] cellIndices;

    // Session-only state. Never serialized into the source ScriptableObject asset.
    [NonSerialized] private RuntimeCache _runtime;

    public RuntimeCache Runtime
    {
        get
        {
            if (_runtime == null || !_runtime.Matches(this))
                _runtime = new RuntimeCache(this);
            return _runtime;
        }
    }

    /// <summary>Call after editing existing array elements in place.</summary>
    public void InvalidateRuntimeCache() => _runtime = null;

    /// <summary>Reset assignment state only; preserve geometry and spatial index.</summary>
    public void ResetRuntimeDestruction()
    {
        if (_runtime != null) _runtime.Destroyed.SetAll(false);
    }

    public sealed class RuntimeCache
    {
        public readonly int Count;
        public readonly BitArray Destroyed;
        public readonly float[] Radii;
        public readonly float MaxRadius;
        public readonly Bounds CenterBounds;
        public readonly Bounds CollisionBounds;
        public readonly Vector3 GridOrigin;
        public readonly float InverseCellSize;
        public readonly Vector3Int MinCell;
        public readonly Vector3Int MaxCell;
        public readonly int[] CellStarts;
        public readonly int[] CellIndices;
        public readonly bool UsesBakedIndex;

        private readonly Dictionary<long, int> _cellLookup;
        private readonly int _sourceCount;
        private readonly Vector3[] _positions;
        private readonly float[] _scales;
        private readonly float[] _baseRadii;
        private readonly long[] _sourceKeys;
        private readonly int[] _sourceStarts;
        private readonly int[] _sourceIndices;
        private readonly float _sourceCellSize;

        internal bool Matches(AsteroidFieldData data)
        {
            return _sourceCount == data.count && _positions == data.positions &&
                _scales == data.scales && _baseRadii == data.baseRadii &&
                _sourceKeys == data.cellKeys && _sourceStarts == data.cellStarts &&
                _sourceIndices == data.cellIndices && _sourceCellSize == data.cellSize &&
                GridOrigin.Equals(data.gridOrigin);
        }

        internal RuntimeCache(AsteroidFieldData data)
        {
            _sourceCount = data.count;
            _positions = data.positions;
            _scales = data.scales;
            _baseRadii = data.baseRadii;
            _sourceKeys = data.cellKeys;
            _sourceStarts = data.cellStarts;
            _sourceIndices = data.cellIndices;
            _sourceCellSize = data.cellSize;
            Count = Mathf.Min(Mathf.Max(0, data.count), data.positions != null ? data.positions.Length : 0);
            Destroyed = new BitArray(Count, false);
            Radii = new float[Count];
            GridOrigin = data.gridOrigin;
            InverseCellSize = 1f / Mathf.Max(0.0001f, data.cellSize);

            Bounds centers = default;
            Bounds collision = default;
            Vector3Int minCell = default;
            Vector3Int maxCell = default;
            float maxRadius = 0f;
            for (int i = 0; i < Count; i++)
            {
                float baseRadius = data.baseRadii != null && i < data.baseRadii.Length
                    ? Mathf.Max(0.0001f, data.baseRadii[i]) : 1f;
                float scale = data.scales != null && i < data.scales.Length ? data.scales[i] : 1f;
                float radius = baseRadius * Mathf.Abs(scale);
                Radii[i] = radius;
                maxRadius = Mathf.Max(maxRadius, radius);
                Vector3 position = data.positions[i];
                Vector3 extent = Vector3.one * radius;
                Vector3Int cell = ToCell(position);
                if (i == 0)
                {
                    centers = new Bounds(position, Vector3.zero);
                    collision = new Bounds(position, extent * 2f);
                    minCell = maxCell = cell;
                }
                else
                {
                    centers.Encapsulate(position);
                    collision.Encapsulate(position - extent);
                    collision.Encapsulate(position + extent);
                    minCell = Vector3Int.Min(minCell, cell);
                    maxCell = Vector3Int.Max(maxCell, cell);
                }
            }
            MaxRadius = maxRadius;
            CenterBounds = centers;
            CollisionBounds = collision;
            MinCell = minCell;
            MaxCell = maxCell;

            long[] keys;
            UsesBakedIndex = HasValidBakedIndex(data);
            if (UsesBakedIndex)
            {
                keys = data.cellKeys;
                CellStarts = data.cellStarts;
                CellIndices = data.cellIndices;
            }
            else
            {
                // Older/non-baked data builds a compact index once, never on chunk entry.
                var cells = new Dictionary<long, List<int>>();
                for (int i = 0; i < Count; i++)
                {
                    Vector3Int cell = ToCell(data.positions[i]);
                    long key = PackCell(cell.x, cell.y, cell.z);
                    if (!cells.TryGetValue(key, out var indices))
                    {
                        indices = new List<int>();
                        cells.Add(key, indices);
                    }
                    indices.Add(i);
                }
                keys = new long[cells.Count];
                cells.Keys.CopyTo(keys, 0);
                Array.Sort(keys);
                CellStarts = new int[keys.Length + 1];
                CellIndices = new int[Count];
                int write = 0;
                for (int k = 0; k < keys.Length; k++)
                {
                    CellStarts[k] = write;
                    List<int> indices = cells[keys[k]];
                    for (int j = 0; j < indices.Count; j++) CellIndices[write++] = indices[j];
                }
                CellStarts[keys.Length] = write;
            }

            // Reuse the baked ranges; this map only accelerates cell-key lookup.
            _cellLookup = new Dictionary<long, int>(keys.Length);
            for (int k = 0; k < keys.Length; k++) _cellLookup.Add(keys[k], k);
        }

        private bool HasValidBakedIndex(AsteroidFieldData data)
        {
            if (data.cellKeys == null || data.cellStarts == null || data.cellIndices == null ||
                data.cellStarts.Length != data.cellKeys.Length + 1 ||
                data.cellIndices.Length != Count || data.cellStarts[0] != 0 ||
                data.cellStarts[data.cellKeys.Length] != Count) return false;

            // One-time validation also detects a stale bake after replacing positions.
            var seen = new BitArray(Count);
            for (int k = 0; k < data.cellKeys.Length; k++)
            {
                if (k > 0 && data.cellKeys[k] <= data.cellKeys[k - 1]) return false;
                int start = data.cellStarts[k];
                int end = data.cellStarts[k + 1];
                if (start < 0 || end < start || end > Count) return false;
                for (int j = start; j < end; j++)
                {
                    int i = data.cellIndices[j];
                    if ((uint)i >= (uint)Count || seen[i]) return false;
                    seen[i] = true;
                    Vector3Int cell = ToCell(data.positions[i]);
                    if (PackCell(cell.x, cell.y, cell.z) != data.cellKeys[k]) return false;
                }
            }
            return true;
        }

        public Vector3Int ToCell(Vector3 localPosition)
        {
            Vector3 p = localPosition - GridOrigin;
            return new Vector3Int(Mathf.FloorToInt(p.x * InverseCellSize),
                Mathf.FloorToInt(p.y * InverseCellSize), Mathf.FloorToInt(p.z * InverseCellSize));
        }

        public bool TryGetCellRange(int x, int y, int z, out int start, out int end)
        {
            if (_cellLookup.TryGetValue(PackCell(x, y, z), out int cell))
            {
                start = CellStarts[cell];
                end = CellStarts[cell + 1];
                return true;
            }
            start = end = 0;
            return false;
        }

        private static long PackCell(int x, int y, int z)
        {
            const int bias = 1 << 20;
            const long mask = (1L << 21) - 1L;
            long lx = ((long)(x + bias)) & mask;
            long ly = ((long)(y + bias)) & mask;
            long lz = ((long)(z + bias)) & mask;
            return lx | (ly << 21) | (lz << 42);
        }
    }

    public void Clear()
    {
        InvalidateRuntimeCache();
        count = 0;

        positions = Array.Empty<Vector3>();
        rotations = Array.Empty<Quaternion>();
        scales = Array.Empty<float>();
        typeIds = Array.Empty<int>();
        angularVelocityDeg = Array.Empty<Vector3>();
        baseRadii = Array.Empty<float>();

        cellSize = 20f;
        gridOrigin = Vector3.zero;
        cellKeys = Array.Empty<long>();
        cellStarts = Array.Empty<int>();
        cellIndices = Array.Empty<int>();
    }
}
