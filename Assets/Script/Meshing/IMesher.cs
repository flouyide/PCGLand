using System.Threading;

namespace PCGLand
{
    /// <summary>
    /// 网格化阶段抽象：给定密度场与分块坐标，产出 MeshData。
    /// 与分块/流式层解耦，可替换为 Marching Cubes 等其他实现。
    /// 实现必须线程安全且不触碰 Unity 对象（仅纯数学 + 托管数组）。
    /// </summary>
    public interface IMesher
    {
        MeshData Build(IDensityField field, ChunkCoord coord, WorldSettings settings, CancellationToken token);
    }
}
