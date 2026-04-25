using EntitasLite;
using UnityEngine;

namespace EntitasLite.Example
{
    // ========== 组件定义 ==========

    /// <summary>
    /// 位置组件
    /// </summary>
    public class PositionComponent : IComponent
    {
        public Vector3 Value;
    }

    /// <summary>
    /// 速度组件
    /// </summary>
    public class VelocityComponent : IComponent
    {
        public Vector3 Value;
    }

    /// <summary>
    /// 生命值组件
    /// </summary>
    public class HealthComponent : IComponent
    {
        public int Max;
        public int Current;
    }

    /// <summary>
    /// 玩家标记组件（空组件，仅作标记）
    /// </summary>
    public class PlayerComponent : IComponent { }

    /// <summary>
    /// 敌人标记组件
    /// </summary>
    public class EnemyComponent : IComponent { }

    /// <summary>
    /// 销毁标记组件
    /// </summary>
    public class DestroyComponent : IComponent { }

    /// <summary>
    /// 精灵渲染组件
    /// </summary>
    public class SpriteComponent : IComponent
    {
        public string SpritePath;
        public Color Color;
    }
}
