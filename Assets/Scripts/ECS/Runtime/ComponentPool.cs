using System;
using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 组件对象池，按组件类型自动隔离池。
    /// 使用方式：
    ///   var comp = ComponentPool&lt;PositionComponent&gt;.Get();
    ///   comp.Value = newValue;
    ///   entity.ReplaceComponent(comp);
    ///   // 旧组件自动回收：
    ///   ComponentPool&lt;PositionComponent&gt;.Return(oldComp);
    /// </summary>
    public static class ComponentPool<T> where T : IComponent, new()
    {
        private static readonly Stack<T> m_Pool = new Stack<T>();
        private static int m_CreatedCount;
        private static int m_ReusedCount;

        /// <summary>
        /// 池中当前可用数量
        /// </summary>
        public static int Count => m_Pool.Count;

        /// <summary>
        /// 累计创建的新组件数量
        /// </summary>
        public static int CreatedCount => m_CreatedCount;

        /// <summary>
        /// 累计复用的组件数量
        /// </summary>
        public static int ReusedCount => m_ReusedCount;

        /// <summary>
        /// 从池中获取组件（池空则创建新实例）
        /// </summary>
        public static T Get()
        {
            if (m_Pool.Count > 0)
            {
                m_ReusedCount++;
                return m_Pool.Pop();
            }

            m_CreatedCount++;
            return new T();
        }

        /// <summary>
        /// 从池中获取组件并初始化
        /// </summary>
        public static T Get(Action<T> init)
        {
            var component = Get();
            init?.Invoke(component);
            return component;
        }

        /// <summary>
        /// 将组件归还到池中
        /// </summary>
        public static void Return(T component)
        {
            if (component == null) return;
            m_Pool.Push(component);
        }

        /// <summary>
        /// 清空池中所有组件
        /// </summary>
        public static void Clear()
        {
            m_Pool.Clear();
        }

        /// <summary>
        /// 重置统计计数
        /// </summary>
        public static void ResetStats()
        {
            m_CreatedCount = 0;
            m_ReusedCount = 0;
        }
    }

    /// <summary>
    /// 非泛型组件池工具，用于统一清空所有池
    /// </summary>
    public static class ComponentPool
    {
        /// <summary>
        /// 清空所有组件类型的对象池（慎用）
        /// </summary>
        public static void ClearAll()
        {
            // 由于泛型静态类每个类型独立，无法统一遍历清空
            // 此方法作为占位，实际由具体类型的 Clear() 处理
        }
    }
}
