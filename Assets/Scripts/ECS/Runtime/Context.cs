using System;
using System.Collections.Generic;
using UnityEngine;

namespace EntitasLite
{
    /// <summary>
    /// 上下文，实体的工厂和管理器，同时整合系统容器，降低业务开发门槛
    /// 无需预先指定组件总数，组件类型自动发现，实体数组动态扩容
    /// </summary>
    public sealed class Context
    {
        /// <summary>
        /// 上下文名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 当前活跃实体数量
        /// </summary>
        public int Count => m_Entities.Count;

        /// <summary>
        /// 内部系统容器（只读访问，高级场景可用）
        /// </summary>
        public Systems Systems => m_Systems;

        private readonly List<Entity> m_Entities;
        private readonly Stack<Entity> m_EntityPool;
        private readonly Dictionary<Matcher, Group> m_Groups;
        private readonly Dictionary<int, List<Group>> m_GroupsForIndex; // 每个组件索引对应的Group列表，动态管理
        private readonly Systems m_Systems;

        // 事件
        public event Action OnEntityCreated;
        public event Action OnEntityWillBeDestroyed;
        public event Action OnEntityDestroyed;
        public event Action OnGroupCreated;

        public Context(string name)
        {
            Name = name;
            m_Entities = new List<Entity>();
            m_EntityPool = new Stack<Entity>();
            m_Groups = new Dictionary<Matcher, Group>();
            m_GroupsForIndex = new Dictionary<int, List<Group>>();
            m_Systems = new Systems();
            ContextRegistry.Register(this);
        }

        #region 实体管理

        /// <summary>
        /// 创建新实体
        /// </summary>
        public Entity CreateEntity()
        {
            Entity entity;
            if (m_EntityPool.Count > 0)
            {
                entity = m_EntityPool.Pop();
                entity.Reactivate();
            }
            else
            {
                entity = new Entity(Name);
            }

            entity.ContextIndex = m_Entities.Count;
            m_Entities.Add(entity);
            entity.OnComponentAdded += OnEntityComponentAdded;
            entity.OnComponentRemoved += OnEntityComponentRemoved;
            entity.OnComponentReplaced += OnEntityComponentReplaced;
            entity.OnEntityReleased += () => OnEntityReleased(entity);

            OnEntityCreated?.Invoke();
            return entity;
        }

        /// <summary>
        /// 判断实体是否属于此上下文（O(1)）
        /// </summary>
        public bool HasEntity(Entity entity)
        {
            var index = entity.ContextIndex;
            return index >= 0 && index < m_Entities.Count && m_Entities[index] == entity;
        }

        /// <summary>
        /// 获取所有实体
        /// </summary>
        public Entity[] GetEntities()
        {
            return m_Entities.ToArray();
        }

        /// <summary>
        /// 获取所有实体（填充列表，避免GC）
        /// </summary>
        public List<Entity> GetEntities(List<Entity> buffer)
        {
            buffer.Clear();
            buffer.AddRange(m_Entities);
            return buffer;
        }

        /// <summary>
        /// 获取所有实体（直接返回内部列表，注意只读）
        /// </summary>
        public IReadOnlyList<Entity> GetEntitiesDirect()
        {
            return m_Entities;
        }

        /// <summary>
        /// 销毁实体（O(1) swap-back 移除）
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            var index = entity.ContextIndex;
            if (index < 0 || index >= m_Entities.Count || m_Entities[index] != entity)
                throw new Exception($"Cannot destroy entity {entity} - not in context {Name}");

            OnEntityWillBeDestroyed?.Invoke();

            // swap-back 移除：把最后一个实体移到当前位置，保持数组连续
            var lastIndex = m_Entities.Count - 1;
            if (index < lastIndex)
            {
                var lastEntity = m_Entities[lastIndex];
                m_Entities[index] = lastEntity;
                lastEntity.ContextIndex = index;
            }
            m_Entities.RemoveAt(lastIndex);
            entity.ContextIndex = -1;

            entity.Destroy();
            OnEntityDestroyed?.Invoke();
        }

        /// <summary>
        /// 销毁所有实体
        /// </summary>
        public void DestroyAllEntities()
        {
            // 倒序遍历，避免 swap-back 导致遍历索引错位
            for (int i = m_Entities.Count - 1; i >= 0; i--)
            {
                DestroyEntity(m_Entities[i]);
            }
        }

        /// <summary>
        /// 获取或创建匹配指定Matcher的组
        /// </summary>
        public Group GetGroup(Matcher matcher)
        {
            if (!m_Groups.TryGetValue(matcher, out var group))
            {
                group = new Group(matcher);
                m_Groups[matcher] = group;

                // 将Group注册到相关组件索引
                foreach (var index in matcher.Indices)
                {
                    if (index >= 0)
                    {
                        if (!m_GroupsForIndex.TryGetValue(index, out var list))
                        {
                            list = new List<Group>();
                            m_GroupsForIndex[index] = list;
                        }
                        list.Add(group);
                    }
                }

                // 将现有实体加入Group
                foreach (var entity in m_Entities)
                {
                    group.HandleEntitySilentlyForCollector(entity);
                }

                OnGroupCreated?.Invoke();
            }
            return group;
        }

        /// <summary>
        /// 清除所有组（慎用，通常在重置上下文时使用）
        /// </summary>
        public void ClearGroups()
        {
            foreach (var group in m_Groups.Values)
            {
                foreach (var index in group.Matcher.Indices)
                {
                    if (index >= 0 && m_GroupsForIndex.TryGetValue(index, out var list))
                    {
                        list.Remove(group);
                    }
                }
            }
            m_Groups.Clear();
        }

        /// <summary>
        /// 重置上下文（销毁所有实体并清空组）
        /// </summary>
        public void Reset()
        {
            DestroyAllEntities();
            ClearGroups();
            m_EntityPool.Clear();
        }

        #endregion

        #region 系统管理（整合Systems以降低使用门槛）

        /// <summary>
        /// 添加系统（支持链式调用）
        /// </summary>
        public Context AddSystem(ISystem system)
        {
            m_Systems.Add(system);
            return this;
        }

        /// <summary>
        /// 初始化所有系统，等效于 Systems.Initialize()
        /// </summary>
        public void Initialize()
        {
            m_Systems.Initialize();
        }

        /// <summary>
        /// 执行所有系统，等效于 Systems.Execute()
        /// </summary>
        public void Execute()
        {
            m_Systems.Execute();
        }

        /// <summary>
        /// 清理所有系统，等效于 Systems.Cleanup()
        /// </summary>
        public void Cleanup()
        {
            m_Systems.Cleanup();
        }

        /// <summary>
        /// 销毁所有系统，等效于 Systems.TearDown()
        /// </summary>
        public void TearDown()
        {
            m_Systems.TearDown();
        }

        #endregion

        private void OnEntityComponentAdded(Entity entity, int index, IComponent component)
        {
            NotifyGroups(index, g => g.HandleEntity(entity));
        }

        private void OnEntityComponentRemoved(Entity entity, int index, IComponent component)
        {
            NotifyGroups(index, g => g.HandleEntitySilently(entity));
        }

        private void OnEntityComponentReplaced(Entity entity, int index, IComponent previousComponent, IComponent newComponent)
        {
            NotifyGroups(index, g => g.UpdateEntity(entity, index, previousComponent, newComponent));
        }

        private void NotifyGroups(int index, Action<Group> action)
        {
            if (index >= 0 && m_GroupsForIndex.TryGetValue(index, out var groups))
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    action(groups[i]);
                }
            }
        }

        private void OnEntityReleased(Entity entity)
        {
            if (!entity.IsEnabled && entity.ContextIndex < 0)
            {
                m_EntityPool.Push(entity);
            }
        }
    }
}
