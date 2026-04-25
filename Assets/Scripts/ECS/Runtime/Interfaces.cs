using System;
using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 组件标记接口，所有组件必须实现此接口
    /// </summary>
    public interface IComponent { }

    /// <summary>
    /// 基础系统接口
    /// </summary>
    public interface ISystem { }

    /// <summary>
    /// 初始化系统，在框架启动时执行一次
    /// </summary>
    public interface IInitializeSystem : ISystem
    {
        void Initialize();
    }

    /// <summary>
    /// 每帧执行系统
    /// </summary>
    public interface IExecuteSystem : ISystem
    {
        void Execute();
    }

    /// <summary>
    /// 清理系统，在Execute之后执行
    /// </summary>
    public interface ICleanupSystem : ISystem
    {
        void Cleanup();
    }

    /// <summary>
    /// 销毁系统，在框架销毁时执行
    /// </summary>
    public interface ITearDownSystem : ISystem
    {
        void TearDown();
    }

    /// <summary>
    /// 响应式系统接口，配合Collector使用
    /// </summary>
    public interface IReactiveSystem : ISystem
    {
        void Execute(List<Entity> entities);
    }

    /// <summary>
    /// 实体变化事件类型
    /// </summary>
    [Flags]
    public enum GroupEventType
    {
        None = 0,
        OnEntityAdded = 1 << 0,
        OnEntityRemoved = 1 << 1,
        OnEntityAddedOrRemoved = OnEntityAdded | OnEntityRemoved,
    }

    /// <summary>
    /// 实体变化委托
    /// </summary>
    public delegate void GroupChanged(Entity entity);

    /// <summary>
    /// 实体更新委托
    /// </summary>
    public delegate void EntityChanged(Entity entity, int index, IComponent component);

    /// <summary>
    /// 组件移除委托
    /// </summary>
    public delegate void ComponentReplaced(Entity entity, int index, IComponent previousComponent, IComponent newComponent);
}
