using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EntitasLite
{
    /// <summary>
    /// 单个系统的执行耗时记录
    /// </summary>
    public readonly struct SystemProfileInfo
    {
        public readonly string Name;
        public readonly long ElapsedMilliseconds;
        public readonly long ElapsedTicks;

        public SystemProfileInfo(string name, long ms, long ticks)
        {
            Name = name;
            ElapsedMilliseconds = ms;
            ElapsedTicks = ticks;
        }
    }

    /// <summary>
    /// 系统容器，管理所有系统的生命周期，支持执行耗时分析
    /// </summary>
    public sealed class Systems : IInitializeSystem, IExecuteSystem, ICleanupSystem, ITearDownSystem
    {
        private readonly List<ISystem> m_Systems;
        private readonly List<IInitializeSystem> m_InitializeSystems;
        private readonly List<IExecuteSystem> m_ExecuteSystems;
        private readonly List<ICleanupSystem> m_CleanupSystems;
        private readonly List<ITearDownSystem> m_TearDownSystems;

        // 耗时分析
        private readonly List<SystemProfileInfo> m_ProfileResults;
        private readonly Stopwatch m_Stopwatch;
        private bool m_ProfilingEnabled;

        /// <summary>
        /// 是否启用执行耗时分析
        /// </summary>
        public bool ProfilingEnabled
        {
            get => m_ProfilingEnabled;
            set => m_ProfilingEnabled = value;
        }

        /// <summary>
        /// 上次 Execute 的各系统耗时（仅 ProfilingEnabled=true 时有值）
        /// </summary>
        public IReadOnlyList<SystemProfileInfo> ProfileResults => m_ProfileResults;

        /// <summary>
        /// 上次 Execute 的总耗时（毫秒）
        /// </summary>
        public long TotalExecuteTimeMs { get; private set; }

        public Systems()
        {
            m_Systems = new List<ISystem>();
            m_InitializeSystems = new List<IInitializeSystem>();
            m_ExecuteSystems = new List<IExecuteSystem>();
            m_CleanupSystems = new List<ICleanupSystem>();
            m_TearDownSystems = new List<ITearDownSystem>();
            m_ProfileResults = new List<SystemProfileInfo>();
            m_Stopwatch = new Stopwatch();
        }

        /// <summary>
        /// 添加系统到容器中
        /// </summary>
        public Systems Add(ISystem system)
        {
            if (system == null)
                throw new ArgumentNullException(nameof(system));

            m_Systems.Add(system);

            if (system is IInitializeSystem init)
                m_InitializeSystems.Add(init);
            if (system is IExecuteSystem exec)
                m_ExecuteSystems.Add(exec);
            if (system is ICleanupSystem cleanup)
                m_CleanupSystems.Add(cleanup);
            if (system is ITearDownSystem tearDown)
                m_TearDownSystems.Add(tearDown);

            return this;
        }

        /// <summary>
        /// 批量添加系统
        /// </summary>
        public Systems Add(params ISystem[] systems)
        {
            foreach (var system in systems)
                Add(system);
            return this;
        }

        /// <summary>
        /// 初始化所有IInitializeSystem
        /// </summary>
        public void Initialize()
        {
            for (int i = 0; i < m_InitializeSystems.Count; i++)
            {
                m_InitializeSystems[i].Initialize();
            }
        }

        /// <summary>
        /// 执行所有IExecuteSystem（支持耗时分析）
        /// </summary>
        public void Execute()
        {
            if (m_ProfilingEnabled)
            {
                m_ProfileResults.Clear();
                m_Stopwatch.Restart();

                for (int i = 0; i < m_ExecuteSystems.Count; i++)
                {
                    long before = m_Stopwatch.ElapsedTicks;
                    m_ExecuteSystems[i].Execute();
                    long after = m_Stopwatch.ElapsedTicks;
                    long ticks = after - before;
                    long ms = ticks * 1000 / Stopwatch.Frequency;
                    m_ProfileResults.Add(new SystemProfileInfo(
                        m_ExecuteSystems[i].GetType().Name, ms, ticks));
                }

                m_Stopwatch.Stop();
                TotalExecuteTimeMs = m_Stopwatch.ElapsedMilliseconds;
            }
            else
            {
                for (int i = 0; i < m_ExecuteSystems.Count; i++)
                {
                    m_ExecuteSystems[i].Execute();
                }
            }
        }

        /// <summary>
        /// 清理所有ICleanupSystem
        /// </summary>
        public void Cleanup()
        {
            for (int i = 0; i < m_CleanupSystems.Count; i++)
            {
                m_CleanupSystems[i].Cleanup();
            }
        }

        /// <summary>
        /// 销毁所有ITearDownSystem
        /// </summary>
        public void TearDown()
        {
            for (int i = 0; i < m_TearDownSystems.Count; i++)
            {
                m_TearDownSystems[i].TearDown();
            }
        }

        /// <summary>
        /// 获取所有系统
        /// </summary>
        public IReadOnlyList<ISystem> GetSystems()
        {
            return m_Systems;
        }

        /// <summary>
        /// 重置耗时统计
        /// </summary>
        public void ResetProfile()
        {
            m_ProfileResults.Clear();
            TotalExecuteTimeMs = 0;
        }
    }
}
