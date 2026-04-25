using System;
using System.Collections;
using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 实体组，缓存匹配某个Matcher的所有实体，支持事件通知
    /// </summary>
    public sealed class Group : IEnumerable<Entity>
    {
        private readonly Matcher m_Matcher;
        private readonly List<Entity> m_Entities;
        private readonly Dictionary<int, Entity> m_EntitiesDict; // 以CreationIndex为键
        private Entity[] m_EntitiesCache;
        private bool m_IsCacheDirty;

        // 实体单例事件
        public event GroupChanged OnEntityAdded;
        public event GroupChanged OnEntityRemoved;
        public event GroupChanged OnEntityUpdated;

        /// <summary>
        /// 当前组内实体数量
        /// </summary>
        public int Count => m_Entities.Count;

        /// <summary>
        /// 组的匹配器
        /// </summary>
        public Matcher Matcher => m_Matcher;

        public Group(Matcher matcher)
        {
            m_Matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
            m_Entities = new List<Entity>();
            m_EntitiesDict = new Dictionary<int, Entity>();
            m_IsCacheDirty = true;
        }

        /// <summary>
        /// 处理实体添加组件事件
        /// </summary>
        internal void HandleEntity(Entity entity)
        {
            if (m_EntitiesDict.ContainsKey(entity.CreationIndex))
            {
                // 实体已在组中，检查是否仍匹配
                if (!m_Matcher.Matches(entity))
                {
                    RemoveEntity(entity);
                }
            }
            else
            {
                // 实体不在组中，检查是否新匹配
                if (m_Matcher.Matches(entity))
                {
                    AddEntity(entity);
                }
            }
        }

        /// <summary>
        /// 处理实体移除组件事件
        /// </summary>
        internal void HandleEntitySilently(Entity entity)
        {
            if (m_EntitiesDict.ContainsKey(entity.CreationIndex) && !m_Matcher.Matches(entity))
            {
                RemoveEntity(entity);
            }
        }

        /// <summary>
        /// 处理实体组件替换事件
        /// </summary>
        internal void UpdateEntity(Entity entity, int index, IComponent previousComponent, IComponent newComponent)
        {
            if (m_EntitiesDict.ContainsKey(entity.CreationIndex))
            {
                OnEntityUpdated?.Invoke(entity);
            }
        }

        /// <summary>
        /// 将实体添加到组（不触发事件，用于初始化）
        /// </summary>
        internal void HandleEntitySilentlyForCollector(Entity entity)
        {
            if (!m_EntitiesDict.ContainsKey(entity.CreationIndex) && m_Matcher.Matches(entity))
            {
                m_Entities.Add(entity);
                m_EntitiesDict[entity.CreationIndex] = entity;
                m_IsCacheDirty = true;
            }
        }

        private void AddEntity(Entity entity)
        {
            m_Entities.Add(entity);
            m_EntitiesDict[entity.CreationIndex] = entity;
            m_IsCacheDirty = true;
            OnEntityAdded?.Invoke(entity);
        }

        private void RemoveEntity(Entity entity)
        {
            // SwapBack 优化：用最后一个元素替换被删除的元素，避免 List.Remove 的中间移位
            int index = m_Entities.IndexOf(entity);
            if (index >= 0)
            {
                int lastIndex = m_Entities.Count - 1;
                if (index != lastIndex)
                {
                    m_Entities[index] = m_Entities[lastIndex];
                }
                m_Entities.RemoveAt(lastIndex);
            }
            m_EntitiesDict.Remove(entity.CreationIndex);
            m_IsCacheDirty = true;
            OnEntityRemoved?.Invoke(entity);
        }

        /// <summary>
        /// 获取组内所有实体（快照数组）
        /// </summary>
        public Entity[] GetEntities()
        {
            if (m_IsCacheDirty)
            {
                m_EntitiesCache = m_Entities.ToArray();
                m_IsCacheDirty = false;
            }
            return m_EntitiesCache;
        }

        /// <summary>
        /// 获取组内所有实体（填充到传入列表，避免GC）
        /// </summary>
        public List<Entity> GetEntities(List<Entity> buffer)
        {
            buffer.Clear();
            buffer.AddRange(m_Entities);
            return buffer;
        }

        /// <summary>
        /// 获取单个实体（如果组内有且仅有一个）
        /// </summary>
        public Entity GetSingleEntity()
        {
            if (Count != 1)
                throw new Exception($"Cannot get single entity from group with {Count} entities");
            return m_Entities[0];
        }

        /// <summary>
        /// 尝试获取单个实体
        /// </summary>
        public bool TryGetSingleEntity(out Entity entity)
        {
            if (Count == 1)
            {
                entity = m_Entities[0];
                return true;
            }
            entity = null;
            return false;
        }

        /// <summary>
        /// 检查是否包含指定实体
        /// </summary>
        public bool ContainsEntity(Entity entity)
        {
            return m_EntitiesDict.ContainsKey(entity.CreationIndex);
        }

        /// <summary>
        /// 获取索引处的实体
        /// </summary>
        public Entity this[int index] => m_Entities[index];

        public IEnumerator<Entity> GetEnumerator() => m_Entities.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString()
        {
            return $"Group({m_Matcher})[{Count}]";
        }
    }
}
