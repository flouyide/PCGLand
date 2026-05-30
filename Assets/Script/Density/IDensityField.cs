using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 体素密度场抽象。约定：density &lt; isoLevel 为实心，&gt;= 为空气。
    /// 实现必须是纯函数（仅依赖 worldPos + 内部种子），以便多线程安全调用。
    /// 这是网格器与噪声实现之间的解耦接口（可替换为 Uber Noise 等）。
    /// </summary>
    public interface IDensityField
    {
        /// <summary>有符号密度。</summary>
        float Sample(Vector3 worldPos);

        /// <summary>梯度（指向密度增大方向），用作 Hermite 法线来源。</summary>
        Vector3 Gradient(Vector3 worldPos);

        /// <summary>该位置的 Biome 颜色，写入顶点色供着色器使用。</summary>
        Color SampleColor(Vector3 worldPos);
    }
}
