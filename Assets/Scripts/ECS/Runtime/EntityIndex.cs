using System;
using System.Collections.Generic;

namespace EntitasLite
{
    /// <summary>
    /// 实体索引，按组件字段值快速查找实体。
    /// 适用于需要按 ID、名称、类型等字段频繁查找的场景。
    /// </summary>
    public class EntityIndex<TComponent, TKey> where TComponent : IComponent
    {
        private readonly Dictionary<TKey, HashSet<Entity>> m_Index;
        private readonly Dictionary<Entity, TKey> m_EntityToKey;
        private readonly Func<TComponent, TKey> m_KeySelector;
        private readonly Group m_Group;

        /// <summary>
        /// 当前索引中不同键的数量
        /// </summary>
        public int KeyCount => m_Index.Count;

        public EntityIndex(Context context, Func<TComponent, TKey> keySelector, Matcher matcher)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (matcher == null) throw new ArgumentNullException(nameof(matcher));

            m_Index = new Dictionary<TKey, HashSet<Entity>>();
            m_EntityToKey = new Dictionary<Entity, TKey>();
            m_KeySelector = keySelector;
            m_Group = context.GetGroup(matcher);

            // 订阅 Group 事件
            m_Group.OnEntityAdded += OnEntityAdded;
            m_Group.OnEntityRemoved += OnEntityRemoved;
            m_Group.OnEntityUpdated += OnEntityUpdated;

            // 索引已有实体
            foreach (var entity in m_Group.GetEntities())
            {
                AddEntity(entity);
            }
        }

        // 空集合缓存，避免每次返回新实例的GC分配
        private static readonly HashSet<Entity> s_EmptySet = new HashSet<Entity>();

        /// <summary>
        /// 根据键获取实体集合（不存在时返回空集合）
        /// </summary>
        public HashSet<Entity> GetEntities(TKey key)
        {
            return m_Index.TryGetValue(key, out var entities)
                ? entities
                : s_EmptySet;
        }

        /// <summary>
        /// 尝试根据键获取实体集合
        /// </summary>
        public bool TryGetEntities(TKey key, out HashSet<Entity> entities)
        {
            return m_Index.TryGetValue(key, out entities);
        }

        /// <summary>
        /// 判断指定键是否存在实体
        /// </summary>
        public bool HasEntities(TKey key)
        {
            return m_Index.TryGetValue(key, out var entities) && entities.Count > 0;
        }

        /// <summary>
        /// 获取所有键
        /// </summary>
        public IEnumerable<TKey> GetKeys()
        {
            return m_Index.Keys;
        }

        /// <summary>
        /// 注销索引（取消事件订阅）
        /// </summary>
        public void Deactivate()
        {
            m_Group.OnEntityAdded -= OnEntityAdded;
            m_Group.OnEntityRemoved -= OnEntityRemoved;
            m_Group.OnEntityUpdated -= OnEntityUpdated;
            m_Index.Clear();
            m_EntityToKey.Clear();
        }

        private void OnEntityAdded(Entity entity)
        {
            AddEntity(entity);
        }

        private void OnEntityRemoved(Entity entity)
        {
            RemoveEntity(entity);
        }

        private void OnEntityUpdated(Entity entity)
        {
            // 组件被替换，先移除旧索引再添加新索引
            RemoveEntity(entity);
            AddEntity(entity);
        }

        private void AddEntity(Entity entity)
        {
            if (!entity.TryGetComponent<TComponent>(out var component))
                return;

            var key = m_KeySelector(component);

            if (!m_Index.TryGetValue(key, out var entities))
            {
                entities = new HashSet<Entity>();
                m_Index[key] = entities;
            }

            entities.Add(entity);
            m_EntityToKey[entity] = key;
        }

        private void RemoveEntity(Entity entity)
        {
            if (!m_EntityToKey.TryGetValue(entity, out var key))
                return;

            if (m_Index.TryGetValue(key, out var entities))
            {
                entities.Remove(entity);
                if (entities.Count == 0)
                    m_Index.Remove(key);
            }

            m_EntityToKey.Remove(entity);
        }
    }

    /// <summary>
    /// 单键实体索引（每个键只对应一个实体，重复键会覆盖）
    /// </summary>
    public class SingleEntityIndex<TComponent, TKey> where TComponent : IComponent
    {
        private readonly Dictionary<TKey, Entity> m_Index;
        private readonly Dictionary<Entity, TKey> m_EntityToKey;
        private readonly Func<TComponent, TKey> m_KeySelector;
        private readonly Group m_Group;

        /// <summary>
        /// 当前索引中不同键的数量
        /// </summary>
        public int KeyCount => m_Index.Count;

        public SingleEntityIndex(Context context, Func<TComponent, TKey> keySelector, Matcher matcher)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (matcher == null) throw new ArgumentNullException(nameof(matcher));

            m_Index = new Dictionary<TKey, Entity>();
            m_EntityToKey = new Dictionary<Entity, TKey>();
            m_KeySelector = keySelector;
            m_Group = context.GetGroup(matcher);

            m_Group.OnEntityAdded += OnEntityAdded;
            m_Group.OnEntityRemoved += OnEntityRemoved;
            m_Group.OnEntityUpdated += OnEntityUpdated;

            foreach (var entity in m_Group.GetEntities())
            {
                AddEntity(entity);
            }
        }

        /// <summary>
        /// 根据键获取实体（不存在时返回 null）
        /// </summary>
        public Entity GetEntity(TKey key)
        {
            return m_Index.TryGetValue(key, out var entity) ? entity : null;
        }

        /// <summary>
        /// 尝试根据键获取实体
        /// </summary>
        public bool TryGetEntity(TKey key, out Entity entity)
        {
            return m_Index.TryGetValue(key, out entity);
        }

        /// <summary>
        /// 判断指定键是否存在
        /// </summary>
        public bool HasEntity(TKey key)
        {
            return m_Index.ContainsKey(key);
        }

        /// <summary>
        /// 获取所有键
        /// </summary>
        public IEnumerable<TKey> GetKeys()
        {
            return m_Index.Keys;
        }

        /// <summary>
        /// 注销索引
        /// </summary>
        public void Deactivate()
        {
            m_Group.OnEntityAdded -= OnEntityAdded;
            m_Group.OnEntityRemoved -= OnEntityRemoved;
            m_Group.OnEntityUpdated -= OnEntityUpdated;
            m_Index.Clear();
            m_EntityToKey.Clear();
        }

        private void OnEntityAdded(Entity entity)
        {
            AddEntity(entity);
        }

        private void OnEntityRemoved(Entity entity)
        {
            RemoveEntity(entity);
        }

        private void OnEntityUpdated(Entity entity)
        {
            RemoveEntity(entity);
            AddEntity(entity);
        }

        private void AddEntity(Entity entity)
        {
            if (!entity.TryGetComponent<TComponent>(out var component))
                return;

            var key = m_KeySelector(component);
            m_Index[key] = entity;
            m_EntityToKey[entity] = key;
        }

        private void RemoveEntity(Entity entity)
        {
            if (!m_EntityToKey.TryGetValue(entity, out var key))
                return;

            m_Index.Remove(key);
            m_EntityToKey.Remove(entity);
        }
    }
}
