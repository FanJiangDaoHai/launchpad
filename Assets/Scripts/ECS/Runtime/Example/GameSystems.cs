using System.Collections.Generic;
using UnityEngine;

namespace EntitasLite.Example
{
    // ========== 系统定义 ==========

    /// <summary>
    /// 初始化系统：创建玩家和敌人实体
    /// </summary>
    public class GameInitSystem : IInitializeSystem
    {
        private readonly Context m_Context;

        public GameInitSystem(Context context)
        {
            m_Context = context;
        }

        public void Initialize()
        {
            // 创建玩家
            var player = m_Context.CreateEntity();
            player.AddComponent(new PositionComponent { Value = Vector3.zero });
            player.AddComponent(new VelocityComponent { Value = new Vector3(2, 0, 0) });
            player.AddComponent(new HealthComponent { Max = 100, Current = 100 });
            player.AddComponent(new PlayerComponent());
            player.AddComponent(new SpriteComponent { Color = Color.blue });

            // 创建敌人
            for (int i = 0; i < 3; i++)
            {
                var enemy = m_Context.CreateEntity();
                enemy.AddComponent(new PositionComponent { Value = new Vector3(-5 + i * 5, 2, 0) });
                enemy.AddComponent(new VelocityComponent { Value = new Vector3(1, 0, 0) });
                enemy.AddComponent(new HealthComponent { Max = 50, Current = 50 });
                enemy.AddComponent(new EnemyComponent());
                enemy.AddComponent(new SpriteComponent { Color = Color.red });
            }

            Debug.Log("[GameInitSystem] 创建了玩家和3个敌人");
        }
    }

    /// <summary>
    /// 移动系统：更新所有有Position和Velocity的实体的位置
    /// </summary>
    public class MoveSystem : IExecuteSystem
    {
        private readonly Group m_MovableGroup;

        public MoveSystem(Context context)
        {
            var matcher = Matcher.AllOf(
                ComponentTypeIndex<PositionComponent>.Index,
                ComponentTypeIndex<VelocityComponent>.Index
            );
            m_MovableGroup = context.GetGroup(matcher);
        }

        public void Execute()
        {
            foreach (var entity in m_MovableGroup.GetEntities())
            {
                var pos = entity.GetComponent<PositionComponent>();
                var vel = entity.GetComponent<VelocityComponent>();
                pos.Value += vel.Value * Time.deltaTime;
            }
        }
    }

    /// <summary>
    /// 边界检测系统：移出边界的实体标记销毁
    /// </summary>
    public class BoundarySystem : IExecuteSystem
    {
        private readonly Context m_Context;
        private readonly Group m_PositionGroup;

        public BoundarySystem(Context context)
        {
            m_Context = context;
            m_PositionGroup = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<PositionComponent>.Index));
        }

        public void Execute()
        {
            foreach (var entity in m_PositionGroup.GetEntities())
            {
                var pos = entity.GetComponent<PositionComponent>();
                if (pos.Value.x > 10 || pos.Value.x < -10)
                {
                    if (!entity.HasComponent<DestroyComponent>())
                    {
                        entity.AddComponent(new DestroyComponent());
                    }
                }
            }
        }
    }

    /// <summary>
    /// 销毁系统：处理所有标记为Destroy的实体
    /// </summary>
    public class DestroySystem : ICleanupSystem
    {
        private readonly Context m_Context;
        private readonly Group m_DestroyGroup;
        private readonly List<Entity> m_Buffer;

        public DestroySystem(Context context)
        {
            m_Context = context;
            m_DestroyGroup = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<DestroyComponent>.Index));
            m_Buffer = new List<Entity>();
        }

        public void Cleanup()
        {
            m_DestroyGroup.GetEntities(m_Buffer);
            for (int i = m_Buffer.Count - 1; i >= 0; i--)
            {
                m_Context.DestroyEntity(m_Buffer[i]);
            }
        }
    }

    /// <summary>
    /// 日志系统：每帧打印玩家位置
    /// </summary>
    public class LogSystem : IExecuteSystem
    {
        private readonly Group m_PlayerGroup;
        private float m_Timer;

        public LogSystem(Context context)
        {
            m_PlayerGroup = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<PlayerComponent>.Index));
        }

        public void Execute()
        {
            m_Timer += Time.deltaTime;
            if (m_Timer >= 2f)
            {
                m_Timer = 0;
                foreach (var entity in m_PlayerGroup.GetEntities())
                {
                    var pos = entity.GetComponent<PositionComponent>();
                    Debug.Log($"[LogSystem] 玩家位置: {pos.Value}");
                }
            }
        }
    }

    /// <summary>
    /// 响应式系统示例：监听新创建的有Health组件的实体
    /// </summary>
    public class HealthAddedSystem : ReactiveSystem
    {
        public HealthAddedSystem(Context context) : base(context) { }

        protected override Matcher GetTrigger()
        {
            return Matcher.AllOf(ComponentTypeIndex<HealthComponent>.Index);
        }

        protected override GroupEventType GetEventType()
        {
            return GroupEventType.OnEntityAdded;
        }

        protected override void Execute(List<Entity> entities)
        {
            foreach (var entity in entities)
            {
                var health = entity.GetComponent<HealthComponent>();
                Debug.Log($"[HealthAddedSystem] 实体 {entity.CreationIndex} 生命值: {health.Current}/{health.Max}");
            }
        }
    }
}
