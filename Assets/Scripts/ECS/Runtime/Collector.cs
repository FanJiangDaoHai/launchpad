using System;
using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 收集器，用于收集满足特定事件的实体变化
    /// </summary>
    public sealed class Collector : IDisposable
    {
        private readonly Group[] m_Groups;
        private readonly GroupEventType[] m_EventTypes;
        private readonly HashSet<Entity> m_CollectedEntities;
        private readonly List<Entity> m_Buffer;
        private bool m_IsActive;

        /// <summary>
        /// 已收集的实体数量
        /// </summary>
        public int Count => m_CollectedEntities.Count;

        public Collector(Group group, GroupEventType eventType)
            : this(new[] { group }, new[] { eventType })
        { }

        public Collector(Group[] groups, GroupEventType[] eventTypes)
        {
            if (groups == null || groups.Length == 0)
                throw new Exception("Collector groups cannot be null or empty");
            if (eventTypes == null || eventTypes.Length == 0)
                throw new Exception("Collector eventTypes cannot be null or empty");
            if (groups.Length != eventTypes.Length)
                throw new Exception("Collector groups and eventTypes must have the same length");

            m_Groups = groups;
            m_EventTypes = eventTypes;
            m_CollectedEntities = new HashSet<Entity>();
            m_Buffer = new List<Entity>();

            Activate();
        }

        /// <summary>
        /// 激活收集器
        /// </summary>
        public void Activate()
        {
            if (m_IsActive) return;
            m_IsActive = true;

            for (int i = 0; i < m_Groups.Length; i++)
            {
                var group = m_Groups[i];
                var eventType = m_EventTypes[i];

                if ((eventType & GroupEventType.OnEntityAdded) != 0)
                    group.OnEntityAdded += AddEntity;
                if ((eventType & GroupEventType.OnEntityRemoved) != 0)
                    group.OnEntityRemoved += AddEntity;
            }
        }

        /// <summary>
        /// 停用收集器
        /// </summary>
        public void Deactivate()
        {
            if (!m_IsActive) return;
            m_IsActive = false;

            for (int i = 0; i < m_Groups.Length; i++)
            {
                var group = m_Groups[i];
                var eventType = m_EventTypes[i];

                if ((eventType & GroupEventType.OnEntityAdded) != 0)
                    group.OnEntityAdded -= AddEntity;
                if ((eventType & GroupEventType.OnEntityRemoved) != 0)
                    group.OnEntityRemoved -= AddEntity;
            }
        }

        /// <summary>
        /// 清空已收集的实体
        /// </summary>
        public void ClearCollectedEntities()
        {
            foreach (var entity in m_CollectedEntities)
            {
                if (entity.IsEnabled)
                    entity.OnEntityReleased -= OnEntityReleased;
            }
            m_CollectedEntities.Clear();
        }

        /// <summary>
        /// 获取已收集的实体
        /// </summary>
        public List<Entity> GetCollectedEntities()
        {
            m_Buffer.Clear();
            m_Buffer.AddRange(m_CollectedEntities);
            return m_Buffer;
        }

        /// <summary>
        /// 获取已收集的实体（快照数组）
        /// </summary>
        public Entity[] GetCollectedEntitiesArray()
        {
            var result = new Entity[m_CollectedEntities.Count];
            m_CollectedEntities.CopyTo(result);
            return result;
        }

        private void AddEntity(Entity entity)
        {
            if (m_CollectedEntities.Add(entity))
            {
                entity.OnEntityReleased += OnEntityReleased;
            }
        }

        private void OnEntityReleased()
        {
            // Entity.OnEntityReleased 是无参委托，
            // 这里用lambda捕获实体实例在Remove时会有GC问题。
            // 简单方案：延迟清理，在Clear时处理
        }

        public void Dispose()
        {
            Deactivate();
            ClearCollectedEntities();
        }
    }
}
