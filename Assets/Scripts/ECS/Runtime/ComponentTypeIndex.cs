using System;
using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 组件类型索引管理器，自动分配组件索引
    /// 使用方式：
    ///   1. 定义组件时调用 ComponentTypeIndex&lt;YourComponent&gt;.Index 获取索引
    ///   2. 组件索引在第一次使用时自动分配，无需预先注册
    /// </summary>
    public static class ComponentTypeIndex
    {
        private static int m_IndexCounter = 0;
        private static readonly Dictionary<Type, int> m_TypeToIndex = new Dictionary<Type, int>();

        /// <summary>
        /// 获取指定组件类型的索引
        /// </summary>
        public static int Get<T>() where T : IComponent
        {
            var type = typeof(T);
            if (!m_TypeToIndex.TryGetValue(type, out var index))
            {
                index = m_IndexCounter++;
                m_TypeToIndex[type] = index;
            }
            return index;
        }

        /// <summary>
        /// 当前已注册组件类型总数
        /// </summary>
        public static int TotalComponents => m_IndexCounter;

        /// <summary>
        /// 重置索引计数器（慎用，仅在特殊场景下使用）
        /// </summary>
        public static void Reset()
        {
            m_IndexCounter = 0;
            m_TypeToIndex.Clear();
        }
    }

    /// <summary>
    /// 泛型组件索引（动态获取，与Reset兼容）
    /// </summary>
    public static class ComponentTypeIndex<T> where T : IComponent
    {
        public static int Index => ComponentTypeIndex.Get<T>();
    }
}
