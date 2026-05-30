using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCGLand
{
    /// <summary>工作线程产出的结果。</summary>
    public struct GenerationResult
    {
        public ChunkCoord Coord;
        public MeshData Mesh;
    }

    /// <summary>
    /// 纯 C# Task 工作池：在后台线程运行 IMesher.Build（仅纯数学，不触碰 Unity API），
    /// 结果推入并发队列供主线程消费。并发数由 SemaphoreSlim 限制；
    /// 每个分块携带 CancellationToken，离开视野时取消。
    /// </summary>
    public sealed class GenerationScheduler : IDisposable
    {
        private readonly IDensityField _field;
        private readonly IMesher _mesher;
        private readonly WorldSettings _settings;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<GenerationResult> _results = new ConcurrentQueue<GenerationResult>();
        private readonly Dictionary<ChunkCoord, CancellationTokenSource> _inFlight = new Dictionary<ChunkCoord, CancellationTokenSource>();
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();

        public GenerationScheduler(IDensityField field, IMesher mesher, WorldSettings settings, int maxConcurrency)
        {
            _field = field;
            _mesher = mesher;
            _settings = settings;
            _semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency));
        }

        public bool IsPending(ChunkCoord coord)
        {
            lock (_lock) return _inFlight.ContainsKey(coord);
        }

        public void Enqueue(ChunkCoord coord)
        {
            CancellationTokenSource cts;
            lock (_lock)
            {
                if (_inFlight.ContainsKey(coord)) return;
                cts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                _inFlight[coord] = cts;
            }

            _ = RunAsync(coord, cts.Token);
        }

        private async Task RunAsync(ChunkCoord coord, CancellationToken token)
        {
            try
            {
                await _semaphore.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    if (token.IsCancellationRequested) return;
                    MeshData mesh = await Task.Run(() => _mesher.Build(_field, coord, _settings, token), token)
                        .ConfigureAwait(false);
                    if (mesh != null && !token.IsCancellationRequested)
                    {
                        _results.Enqueue(new GenerationResult { Coord = coord, Mesh = mesh });
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略。
            }
            finally
            {
                lock (_lock)
                {
                    if (_inFlight.TryGetValue(coord, out var cts) && cts.Token == token)
                    {
                        _inFlight.Remove(coord);
                        cts.Dispose();
                    }
                }
            }
        }

        /// <summary>取消某分块的在途生成（离开视野时调用）。</summary>
        public void Cancel(ChunkCoord coord)
        {
            lock (_lock)
            {
                if (_inFlight.TryGetValue(coord, out var cts))
                {
                    cts.Cancel();
                }
            }
        }

        public void CancelExcept(HashSet<ChunkCoord> desired)
        {
            lock (_lock)
            {
                foreach (var kv in _inFlight)
                {
                    if (!desired.Contains(kv.Key))
                    {
                        kv.Value.Cancel();
                    }
                }
            }
        }

        /// <summary>主线程调用：取出一个已完成结果。</summary>
        public bool TryDequeue(out GenerationResult result) => _results.TryDequeue(out result);

        public void Dispose()
        {
            _shutdown.Cancel();
            lock (_lock)
            {
                foreach (var kv in _inFlight) kv.Value.Dispose();
                _inFlight.Clear();
            }
            _shutdown.Dispose();
            _semaphore.Dispose();
        }
    }
}
