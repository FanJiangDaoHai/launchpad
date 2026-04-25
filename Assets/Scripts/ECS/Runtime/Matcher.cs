using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EntitasLite
{
    /// <summary>
    /// 匹配器，用于筛选实体
    /// </summary>
    public sealed class Matcher
    {
        /// <summary>
        /// 必须包含的所有组件索引
        /// </summary>
        public int[] AllOfIndices { get; private set; }

        /// <summary>
        /// 至少包含其中一个的组件索引
        /// </summary>
        public int[] AnyOfIndices { get; private set; }

        /// <summary>
        /// 必须不包含的组件索引
        /// </summary>
        public int[] NoneOfIndices { get; private set; }

        private int[] m_Indices;
        private int m_Hash;
        private string m_ToStringCache;

        private Matcher() { }

        /// <summary>
        /// 获取所有涉及的组件索引（去重排序）
        /// </summary>
        public int[] Indices
        {
            get
            {
                if (m_Indices == null)
                {
                    var hashSet = new HashSet<int>();
                    if (AllOfIndices != null)
                        foreach (var i in AllOfIndices) hashSet.Add(i);
                    if (AnyOfIndices != null)
                        foreach (var i in AnyOfIndices) hashSet.Add(i);
                    if (NoneOfIndices != null)
                        foreach (var i in NoneOfIndices) hashSet.Add(i);
                    m_Indices = hashSet.ToArray();
                    Array.Sort(m_Indices);
                }
                return m_Indices;
            }
        }

        /// <summary>
        /// 检查实体是否匹配此匹配器
        /// </summary>
        public bool Matches(Entity entity)
        {
            // 使用局部变量避免属性多次访问，减少分支预测失败
            var allOf = AllOfIndices;
            var anyOf = AnyOfIndices;
            var noneOf = NoneOfIndices;

            if (allOf != null && !entity.HasComponents(allOf))
                return false;
            if (anyOf != null && !entity.HasAnyComponent(anyOf))
                return false;
            if (noneOf != null && entity.HasAnyComponent(noneOf))
                return false;

            return true;
        }

        /// <summary>
        /// 创建必须包含所有指定组件的匹配器
        /// </summary>
        public static Matcher AllOf(params int[] indices)
        {
            if (indices == null || indices.Length == 0)
                throw new Exception("Matcher.AllOf indices cannot be null or empty");

            return new Matcher
            {
                AllOfIndices = DistinctIndices(indices)
            };
        }

        /// <summary>
        /// 创建至少包含一个指定组件的匹配器
        /// </summary>
        public static Matcher AnyOf(params int[] indices)
        {
            if (indices == null || indices.Length == 0)
                throw new Exception("Matcher.AnyOf indices cannot be null or empty");

            return new Matcher
            {
                AnyOfIndices = DistinctIndices(indices)
            };
        }

        /// <summary>
        /// 添加必须不包含的组件条件
        /// </summary>
        public Matcher NoneOf(params int[] indices)
        {
            if (indices == null || indices.Length == 0)
                throw new Exception("Matcher.NoneOf indices cannot be null or empty");

            NoneOfIndices = DistinctIndices(indices);
            m_Indices = null;
            m_ToStringCache = null;
            return this;
        }

        private static int[] DistinctIndices(int[] indices)
        {
            var distinct = indices.Distinct().ToArray();
            Array.Sort(distinct);
            return distinct;
        }

        public override int GetHashCode()
        {
            if (m_Hash == 0)
            {
                unchecked
                {
                    m_Hash = 17;
                    if (AllOfIndices != null)
                        foreach (var i in AllOfIndices)
                            m_Hash = m_Hash * 31 + i;
                    m_Hash = m_Hash * 31 + 37;
                    if (AnyOfIndices != null)
                        foreach (var i in AnyOfIndices)
                            m_Hash = m_Hash * 31 + i;
                    m_Hash = m_Hash * 31 + 53;
                    if (NoneOfIndices != null)
                        foreach (var i in NoneOfIndices)
                            m_Hash = m_Hash * 31 + i;
                }
            }
            return m_Hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Matcher other))
                return false;

            return GetHashCode() == other.GetHashCode()
                && IndicesEqual(AllOfIndices, other.AllOfIndices)
                && IndicesEqual(AnyOfIndices, other.AnyOfIndices)
                && IndicesEqual(NoneOfIndices, other.NoneOfIndices);
        }

        private static bool IndicesEqual(int[] a, int[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public override string ToString()
        {
            if (m_ToStringCache == null)
            {
                var sb = new StringBuilder();
                sb.Append("Matcher(");
                if (AllOfIndices != null)
                {
                    sb.Append("AllOf(");
                    sb.Append(string.Join(",", AllOfIndices));
                    sb.Append(")");
                }
                if (AnyOfIndices != null)
                {
                    if (AllOfIndices != null) sb.Append(".");
                    sb.Append("AnyOf(");
                    sb.Append(string.Join(",", AnyOfIndices));
                    sb.Append(")");
                }
                if (NoneOfIndices != null)
                {
                    sb.Append(".NoneOf(");
                    sb.Append(string.Join(",", NoneOfIndices));
                    sb.Append(")");
                }
                sb.Append(")");
                m_ToStringCache = sb.ToString();
            }
            return m_ToStringCache;
        }
    }
}
