using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 流式加载管理器（主线程）。每帧：
    /// 1) 计算观察者半径内所需分块集合；
    /// 2) 把缺失分块入队到 GenerationScheduler；
    /// 3) 在每帧预算内消费完成的结果并上传 Mesh；
    /// 4) 卸载半径外分块、取消其在途任务。
    /// </summary>
    public sealed class ChunkManager : MonoBehaviour
    {
        private WorldSettings _settings;
        private IDensityField _field;
        private IMesher _mesher;
        private Transform _viewer;
        private Material _material;

        private GenerationScheduler _scheduler;
        private ChunkPool _pool;

        private readonly Dictionary<ChunkCoord, Chunk> _active = new Dictionary<ChunkCoord, Chunk>();
        private readonly HashSet<ChunkCoord> _desired = new HashSet<ChunkCoord>();
        private readonly List<ChunkCoord> _toEnqueue = new List<ChunkCoord>();
        private readonly List<ChunkCoord> _toUnload = new List<ChunkCoord>();

        private bool _initialized;
        private bool _hasCenter;
        private ChunkCoord _center;

        public int ActiveCount => _active.Count;

        public void Initialize(WorldSettings settings, IDensityField field, IMesher mesher,
            Transform viewer, Material material)
        {
            Shutdown();

            _settings = settings;
            _field = field;
            _mesher = mesher;
            _viewer = viewer;
            _material = material;

            int workers = Math.Max(1, Environment.ProcessorCount - 1);
            _scheduler = new GenerationScheduler(field, mesher, settings, workers);
            _pool = new ChunkPool(transform, material);
            _hasCenter = false;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _viewer == null) return;

            ChunkCoord center = ChunkCoord.FromWorld(_viewer.position, _settings.chunkSize);
            if (!_hasCenter || !center.Equals(_center))
            {
                _center = center;
                _hasCenter = true;
                RecomputeDesired();
                EnqueueMissing();
                UnloadOutOfRange();
            }

            DrainResults();
        }

        private void RecomputeDesired()
        {
            _desired.Clear();
            int r = _settings.viewRadiusChunks;
            int vr = _settings.verticalRadiusChunks;
            int rSqr = r * r;
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    if (dx * dx + dz * dz > rSqr) continue; // 水平方向用圆形
                    for (int dy = -vr; dy <= vr; dy++)
                    {
                        _desired.Add(new ChunkCoord(_center.x + dx, _center.y + dy, _center.z + dz));
                    }
                }
            }
        }

        private void EnqueueMissing()
        {
            _toEnqueue.Clear();
            foreach (var coord in _desired)
            {
                if (_active.ContainsKey(coord)) continue;
                if (_scheduler.IsPending(coord)) continue;
                _toEnqueue.Add(coord);
            }

            // 就近优先
            ChunkCoord c = _center;
            _toEnqueue.Sort((a, b) => SqrDist(a, c).CompareTo(SqrDist(b, c)));
            foreach (var coord in _toEnqueue) _scheduler.Enqueue(coord);
        }

        private void UnloadOutOfRange()
        {
            _toUnload.Clear();
            foreach (var kv in _active)
            {
                if (!_desired.Contains(kv.Key)) _toUnload.Add(kv.Key);
            }
            foreach (var coord in _toUnload)
            {
                _scheduler.Cancel(coord);
                _pool.Return(_active[coord]);
                _active.Remove(coord);
            }
        }

        private void DrainResults()
        {
            int budget = _settings.uploadsPerFrame;
            while (budget-- > 0 && _scheduler.TryDequeue(out var result))
            {
                // 结果回来时可能已不再需要，或已存在 → 丢弃。
                if (!_desired.Contains(result.Coord) || _active.ContainsKey(result.Coord))
                {
                    continue;
                }

                Chunk chunk = _pool.Rent(result.Coord, _settings.chunkSize);
                result.Mesh.UploadTo(chunk.Mesh);

                if (_settings.generateColliders && !result.Mesh.IsEmpty)
                {
                    if (chunk.Collider == null)
                        chunk.Collider = chunk.GameObject.AddComponent<MeshCollider>();
                    chunk.Collider.sharedMesh = chunk.Mesh;
                }

                _active[result.Coord] = chunk;
            }
        }

        private static int SqrDist(ChunkCoord a, ChunkCoord b)
        {
            int dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>清空所有分块并重置（用于种子/参数变化后的整体重建）。</summary>
        public void Shutdown()
        {
            if (_scheduler != null)
            {
                _scheduler.Dispose();
                _scheduler = null;
            }
            // 销毁所有分块 GameObject（含池中闲置的），避免重建时泄漏。
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            _active.Clear();
            _pool = null;
            _desired.Clear();
            _hasCenter = false;
            _initialized = false;
        }

        private void OnDestroy() => Shutdown();
    }
}
