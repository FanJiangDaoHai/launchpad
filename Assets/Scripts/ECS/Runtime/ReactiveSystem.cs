using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 响应式系统基类，自动管理 Collector 的创建和生命周期。
    /// 子类只需指定触发条件（Matcher + 事件类型）和处理逻辑。
    /// </summary>
    public abstract class ReactiveSystem : IInitializeSystem, IExecuteSystem
    {
        private readonly Context m_Context;
        private Collector m_Collector;
        private readonly List<Entity> m_Buffer;

        protected ReactiveSystem(Context context)
        {
            m_Context = context;
            m_Buffer = new List<Entity>();
        }

        /// <summary>
        /// 定义触发此响应式系统的 Matcher
        /// </summary>
        protected abstract Matcher GetTrigger();

        /// <summary>
        /// 定义监听的事件类型（Added / Removed / Both）
        /// </summary>
        protected abstract GroupEventType GetEventType();

        /// <summary>
        /// 当收集器中有实体变化时执行
        /// </summary>
        protected abstract void Execute(List<Entity> entities);

        public void Initialize()
        {
            var group = m_Context.GetGroup(GetTrigger());
            m_Collector = new Collector(group, GetEventType());
        }

        public void Execute()
        {
            var entities = m_Collector.GetCollectedEntities();
            if (entities.Count > 0)
            {
                m_Buffer.Clear();
                m_Buffer.AddRange(entities);
                Execute(m_Buffer);
                m_Collector.ClearCollectedEntities();
            }
        }
    }
}
