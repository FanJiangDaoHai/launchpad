using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// Context 全局注册表，用于 Editor 调试窗口遍历所有 Context。
    /// Context 创建时自动注册，销毁时自动注销。
    /// </summary>
    public static class ContextRegistry
    {
        private static readonly List<Context> m_Contexts = new List<Context>();

        /// <summary>
        /// 注册 Context（由 Context 构造函数自动调用）
        /// </summary>
        public static void Register(Context context)
        {
            if (context != null && !m_Contexts.Contains(context))
            {
                m_Contexts.Add(context);
            }
        }

        /// <summary>
        /// 注销 Context
        /// </summary>
        public static void Unregister(Context context)
        {
            if (context != null)
            {
                m_Contexts.Remove(context);
            }
        }

        /// <summary>
        /// 获取所有已注册的 Context
        /// </summary>
        public static IReadOnlyList<Context> GetAllContexts()
        {
            // 过滤掉已被销毁或 null 的 Context
            m_Contexts.RemoveAll(c => c == null);
            return m_Contexts;
        }

        /// <summary>
        /// 清空注册表
        /// </summary>
        public static void Clear()
        {
            m_Contexts.Clear();
        }
    }
}
