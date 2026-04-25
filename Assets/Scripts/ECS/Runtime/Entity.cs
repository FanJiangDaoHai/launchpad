using System;
using System.Collections.Generic;
using System.Text;

namespace EntitasLite
{
    /// <summary>
    /// 实体，ECS中的E，是组件的容器
    /// 组件存储使用数组+位图，O(1)访问且避免Dictionary的GC和哈希开销
    /// 位图使用ulong数组，支持超过64种组件类型
    /// 组件数组动态扩容，无需预先注册组件类型或指定总数
    /// </summary>
    public sealed class Entity
    {
        private static int m_CreationIndex = 0;

        /// <summary>
        /// 实体唯一ID（创建时分配，永不改变）
        /// </summary>
        public int CreationIndex { get; private set; }

        /// <summary>
        /// 实体是否有效（销毁后为false）
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// 所属上下文名称
        /// </summary>
        public string ContextName { get; private set; }

        /// <summary>
        /// 在所属上下文实体列表中的索引，-1表示不在任何上下文中
        /// </summary>
        internal int ContextIndex { get; set; }

        // 组件存储：数组+位图数组，O(1)访问，支持无限组件类型，动态扩容
        private IComponent[] m_Components;
        private ulong[] m_ComponentBits;
        private int m_ComponentCount;

        // 初始容量
        private const int INITIAL_COMPONENT_CAPACITY = 4;

        // 位运算辅助常量
        private const int BITS_PER_ULONG_SHIFT = 6;
        private const int BITS_PER_ULONG_MASK = 63;

        // 事件
        public event EntityChanged OnComponentAdded;
        public event EntityChanged OnComponentRemoved;
        public event ComponentReplaced OnComponentReplaced;
        public event Action OnEntityReleased;

        internal Entity(string contextName)
        {
            ContextName = contextName;
            CreationIndex = m_CreationIndex++;
            ContextIndex = -1;
            m_Components = new IComponent[INITIAL_COMPONENT_CAPACITY];
            m_ComponentBits = new ulong[1];
            m_ComponentCount = 0;
            IsEnabled = true;
        }

        /// <summary>
        /// 当前组件数量
        /// </summary>
        public int ComponentCount => m_ComponentCount;

        /// <summary>
        /// 位图数组（内部使用，高级场景）
        /// </summary>
        internal ulong[] ComponentBits => m_ComponentBits;

        // 内联辅助方法：计算位数组索引和位掩码
        private static int GetBitArrayIndex(int index) => index >> BITS_PER_ULONG_SHIFT;
        private static ulong GetBitMask(int index) => 1UL << (index & BITS_PER_ULONG_MASK);

        /// <summary>
        /// 确保数组容量足够容纳指定索引的组件
        /// </summary>
        private void EnsureCapacity(int index)
        {
            if (index < m_Components.Length)
                return;

            // 扩容组件数组（2倍或满足需求）
            int newComponentSize = Math.Max(m_Components.Length * 2, index + 1);
            var newComponents = new IComponent[newComponentSize];
            Array.Copy(m_Components, newComponents, m_Components.Length);
            m_Components = newComponents;

            // 扩容位图数组
            int requiredBits = (newComponentSize + BITS_PER_ULONG_MASK) >> BITS_PER_ULONG_SHIFT;
            if (requiredBits > m_ComponentBits.Length)
            {
                int newBitSize = Math.Max(m_ComponentBits.Length * 2, requiredBits);
                var newBits = new ulong[newBitSize];
                Array.Copy(m_ComponentBits, newBits, m_ComponentBits.Length);
                m_ComponentBits = newBits;
            }
        }

        /// <summary>
        /// 添加组件（如果不存在则添加，存在则替换）
        /// </summary>
        public Entity AddComponent(int index, IComponent component)
        {
            if (!IsEnabled)
                throw new Exception($"Cannot add component to destroyed entity {this}");

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            EnsureCapacity(index);

            int arrayIndex = GetBitArrayIndex(index);
            ulong mask = GetBitMask(index);

            if ((m_ComponentBits[arrayIndex] & mask) != 0)
            {
                ReplaceComponent(index, component);
                return this;
            }

            m_Components[index] = component;
            m_ComponentBits[arrayIndex] |= mask;
            m_ComponentCount++;
            OnComponentAdded?.Invoke(this, index, component);
            return this;
        }

        /// <summary>
        /// 添加组件（泛型，自动推断组件索引，无需传入index）
        /// </summary>
        public Entity AddComponent<T>(T component) where T : IComponent
        {
            return AddComponent(ComponentTypeIndex<T>.Index, component);
        }

        /// <summary>
        /// 移除组件
        /// </summary>
        public Entity RemoveComponent(int index)
        {
            if (!IsEnabled)
                throw new Exception($"Cannot remove component from destroyed entity {this}");

            if (index < 0 || index >= m_Components.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            int arrayIndex = GetBitArrayIndex(index);
            ulong mask = GetBitMask(index);

            if ((m_ComponentBits[arrayIndex] & mask) == 0)
                throw new Exception($"Cannot remove component at index {index} from {this}");

            var component = m_Components[index];
            m_Components[index] = null;
            m_ComponentBits[arrayIndex] &= ~mask;
            m_ComponentCount--;
            OnComponentRemoved?.Invoke(this, index, component);
            return this;
        }

        /// <summary>
        /// 移除组件（泛型，自动推断组件索引，无需传入index）
        /// </summary>
        public Entity RemoveComponent<T>() where T : IComponent
        {
            return RemoveComponent(ComponentTypeIndex<T>.Index);
        }

        /// <summary>
        /// 替换组件
        /// </summary>
        public Entity ReplaceComponent(int index, IComponent component)
        {
            if (!IsEnabled)
                throw new Exception($"Cannot replace component on destroyed entity {this}");

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            // 索引超出当前容量，说明组件一定不存在，直接添加
            if (index >= m_Components.Length)
            {
                AddComponent(index, component);
                return this;
            }

            int arrayIndex = GetBitArrayIndex(index);
            ulong mask = GetBitMask(index);

            if ((m_ComponentBits[arrayIndex] & mask) != 0)
            {
                var previousComponent = m_Components[index];
                if (previousComponent == component)
                    return this;

                m_Components[index] = component;
                OnComponentReplaced?.Invoke(this, index, previousComponent, component);
            }
            else
            {
                AddComponent(index, component);
            }

            return this;
        }

        /// <summary>
        /// 替换组件（泛型，自动推断组件索引，无需传入index）
        /// </summary>
        public Entity ReplaceComponent<T>(T component) where T : IComponent
        {
            return ReplaceComponent(ComponentTypeIndex<T>.Index, component);
        }

        /// <summary>
        /// 获取组件
        /// </summary>
        public IComponent GetComponent(int index)
        {
            if (index < 0 || index >= m_Components.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            int arrayIndex = GetBitArrayIndex(index);
            ulong mask = GetBitMask(index);

            if ((m_ComponentBits[arrayIndex] & mask) != 0)
                return m_Components[index];

            throw new Exception($"Cannot get component at index {index} from {this}");
        }

        /// <summary>
        /// 获取组件（泛型，需传入index）
        /// </summary>
        public T GetComponent<T>(int index) where T : IComponent
        {
            return (T)GetComponent(index);
        }

        /// <summary>
        /// 获取组件（泛型，自动推断组件索引，无需传入index）
        /// </summary>
        public T GetComponent<T>() where T : IComponent
        {
            return GetComponent<T>(ComponentTypeIndex<T>.Index);
        }

        /// <summary>
        /// 尝试获取组件
        /// </summary>
        public bool TryGetComponent(int index, out IComponent component)
        {
            if (index >= 0 && index < m_Components.Length)
            {
                int arrayIndex = GetBitArrayIndex(index);
                ulong mask = GetBitMask(index);
                if ((m_ComponentBits[arrayIndex] & mask) != 0)
                {
                    component = m_Components[index];
                    return true;
                }
            }
            component = null;
            return false;
        }

        /// <summary>
        /// 尝试获取组件（泛型，自动推断组件索引，无需传入index）
        /// </summary>
        public bool TryGetComponent<T>(out T component) where T : IComponent
        {
            int index = ComponentTypeIndex<T>.Index;
            if (index < m_Components.Length)
            {
                int arrayIndex = GetBitArrayIndex(index);
                ulong mask = GetBitMask(index);
                if ((m_ComponentBits[arrayIndex] & mask) != 0)
                {
                    component = (T)m_Components[index];
                    return true;
                }
            }
            component = default;
            return false;
        }

        /// <summary>
        /// 是否有指定组件
        /// </summary>
        public bool HasComponent(int index)
        {
            if (index < 0 || index >= m_Components.Length)
                return false;

            int arrayIndex = GetBitArrayIndex(index);
            ulong mask = GetBitMask(index);
            return (m_ComponentBits[arrayIndex] & mask) != 0;
        }

        /// <summary>
        /// 是否有指定组件（泛型，自动推断组件索引，无需传入index）
        /// </summary>
        public bool HasComponent<T>() where T : IComponent
        {
            return HasComponent(ComponentTypeIndex<T>.Index);
        }

        /// <summary>
        /// 是否有所有指定组件
        /// </summary>
        public bool HasComponents(int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= m_Components.Length)
                    return false;

                int arrayIndex = GetBitArrayIndex(idx);
                ulong mask = GetBitMask(idx);
                if ((m_ComponentBits[arrayIndex] & mask) == 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 是否有任一指定组件
        /// </summary>
        public bool HasAnyComponent(int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                int idx = indices[i];
                if (idx >= 0 && idx < m_Components.Length)
                {
                    int arrayIndex = GetBitArrayIndex(idx);
                    ulong mask = GetBitMask(idx);
                    if ((m_ComponentBits[arrayIndex] & mask) != 0)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 获取当前所有组件的索引
        /// </summary>
        public int[] GetComponentIndices()
        {
            var result = new int[m_ComponentCount];
            int j = 0;
            for (int i = 0; i < m_Components.Length; i++)
            {
                int arrayIndex = GetBitArrayIndex(i);
                ulong mask = GetBitMask(i);
                if ((m_ComponentBits[arrayIndex] & mask) != 0)
                {
                    result[j++] = i;
                }
            }
            return result;
        }

        /// <summary>
        /// 获取当前所有组件
        /// </summary>
        public IComponent[] GetComponents()
        {
            var result = new IComponent[m_ComponentCount];
            int j = 0;
            for (int i = 0; i < m_Components.Length; i++)
            {
                int arrayIndex = GetBitArrayIndex(i);
                ulong mask = GetBitMask(i);
                if ((m_ComponentBits[arrayIndex] & mask) != 0)
                {
                    result[j++] = m_Components[i];
                }
            }
            return result;
        }

        /// <summary>
        /// 移除所有组件
        /// </summary>
        public void RemoveAllComponents()
        {
            for (int i = 0; i < m_Components.Length; i++)
            {
                int arrayIndex = GetBitArrayIndex(i);
                ulong mask = GetBitMask(i);
                if ((m_ComponentBits[arrayIndex] & mask) != 0)
                {
                    // 先清除位图，确保事件处理期间实体状态一致
                    m_ComponentBits[arrayIndex] &= ~mask;
                    var component = m_Components[i];
                    m_Components[i] = null;
                    OnComponentRemoved?.Invoke(this, i, component);
                }
            }
            m_ComponentCount = 0;
        }

        /// <summary>
        /// 销毁实体（移除所有组件并标记为无效）
        /// </summary>
        internal void Destroy()
        {
            IsEnabled = false;
            RemoveAllComponents();
            OnEntityReleased?.Invoke();
        }

        /// <summary>
        /// 重新初始化（用于对象池回收）
        /// </summary>
        internal void Reactivate()
        {
            IsEnabled = true;
            ContextIndex = -1;
            CreationIndex = m_CreationIndex++;
            Array.Clear(m_ComponentBits, 0, m_ComponentBits.Length);
            Array.Clear(m_Components, 0, m_Components.Length);
            m_ComponentCount = 0;

            OnComponentAdded = null;
            OnComponentRemoved = null;
            OnComponentReplaced = null;
            OnEntityReleased = null;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Entity_{CreationIndex}");
            if (!string.IsNullOrEmpty(ContextName))
                sb.Append($"({ContextName})");
            sb.Append("(");
            bool first = true;
            for (int i = 0; i < m_Components.Length; i++)
            {
                int arrayIndex = GetBitArrayIndex(i);
                ulong mask = GetBitMask(i);
                if ((m_ComponentBits[arrayIndex] & mask) != 0)
                {
                    if (!first) sb.Append(",");
                    sb.Append(i);
                    first = false;
                }
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
}
