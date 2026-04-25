using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EntitasLite.PlayModeTests
{
    // ========== Play Mode 测试用组件 ==========

    public class TransformComp : IComponent
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    public class VelocityComp : IComponent
    {
        public Vector3 Value;
    }

    public class HealthComp : IComponent
    {
        public int Max;
        public int Current;
    }

    public class PlayerTag : IComponent { }
    public class EnemyTag : IComponent { }
    public class DestroyTag : IComponent { }

    // ========== Play Mode 核心测试 ==========

    public class PlayMode_CoreTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [UnityTest]
        public IEnumerator Context_CreateEntity_InPlayMode()
        {
            var context = new Context("PlayTest");
            var entity = context.CreateEntity();

            Assert.IsNotNull(entity);
            Assert.IsTrue(entity.IsEnabled);
            Assert.AreEqual("PlayTest", entity.ContextName);

            yield return null;

            context.DestroyEntity(entity);
        }

        [UnityTest]
        public IEnumerator Entity_AddComponent_And_Query_InPlayMode()
        {
            var context = new Context("PlayTest");
            var entity = context.CreateEntity();

            entity.AddComponent(new TransformComp { Position = Vector3.one, Rotation = Quaternion.identity });
            entity.AddComponent(new VelocityComp { Value = Vector3.up });

            yield return null;

            Assert.IsTrue(entity.HasComponent<TransformComp>());
            Assert.IsTrue(entity.HasComponent<VelocityComp>());

            var transform = entity.GetComponent<TransformComp>();
            Assert.AreEqual(Vector3.one, transform.Position);

            context.DestroyEntity(entity);
        }

        [UnityTest]
        public IEnumerator Group_Updates_Across_Frames()
        {
            var context = new Context("PlayTest");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<TransformComp>.Index));

            var entity = context.CreateEntity();
            entity.AddComponent(new TransformComp { Position = Vector3.zero });

            yield return null;

            Assert.AreEqual(1, group.Count);

            entity.AddComponent(new VelocityComp { Value = Vector3.right });

            yield return null;

            // Group 数量不应变化，因为 Matcher 只匹配 TransformComp
            Assert.AreEqual(1, group.Count);

            context.DestroyEntity(entity);
        }

        [UnityTest]
        public IEnumerator Multiple_Entities_Group_Matching()
        {
            var context = new Context("PlayTest");
            var group = context.GetGroup(Matcher.AllOf(
                ComponentTypeIndex<TransformComp>.Index,
                ComponentTypeIndex<VelocityComp>.Index
            ));

            var entities = new List<Entity>();
            for (int i = 0; i < 5; i++)
            {
                var e = context.CreateEntity();
                e.AddComponent(new TransformComp { Position = new Vector3(i, 0, 0) });
                e.AddComponent(new VelocityComp { Value = Vector3.forward });
                entities.Add(e);
            }

            yield return null;

            Assert.AreEqual(5, group.Count);

            foreach (var e in entities)
            {
                context.DestroyEntity(e);
            }
        }

        [UnityTest]
        public IEnumerator Entity_Destroy_And_Pool_Reuse()
        {
            var context = new Context("PlayTest");
            var entity = context.CreateEntity();
            var creationIndex = entity.CreationIndex;

            entity.AddComponent(new TransformComp { Position = Vector3.one });
            context.DestroyEntity(entity);

            yield return null;

            var newEntity = context.CreateEntity();
            // 对象池回收复用
            Assert.AreNotEqual(creationIndex, newEntity.CreationIndex);
            Assert.IsTrue(newEntity.IsEnabled);
            Assert.AreEqual(0, newEntity.GetComponentIndices().Length);

            context.DestroyEntity(newEntity);
        }

        [UnityTest]
        public IEnumerator Context_Reset_Clears_Everything()
        {
            var context = new Context("PlayTest");
            for (int i = 0; i < 10; i++)
            {
                var e = context.CreateEntity();
                e.AddComponent(new TransformComp { Position = Vector3.one * i });
            }

            yield return null;

            Assert.AreEqual(10, context.Count);

            context.Reset();

            yield return null;

            Assert.AreEqual(0, context.Count);
        }
    }

    // ========== Play Mode 系统生命周期测试 ==========

    public class PlayMode_SystemLifecycleTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [UnityTest]
        public IEnumerator Systems_Initialize_Execute_Cleanup_TearDown()
        {
            var context = new Context("PlayTest");
            var lifecycle = new List<string>();

            context.AddSystem(new LifecycleSystem(lifecycle));
            context.Initialize();

            yield return null;

            Assert.Contains("Init", lifecycle);

            context.Execute();
            Assert.Contains("Execute", lifecycle);

            context.Cleanup();
            Assert.Contains("Cleanup", lifecycle);

            context.TearDown();
            Assert.Contains("TearDown", lifecycle);
        }

        [UnityTest]
        public IEnumerator ExecuteSystem_Runs_Every_Frame()
        {
            var context = new Context("PlayTest");
            var counter = new CounterSystem();

            context.AddSystem(counter);
            context.Initialize();

            for (int i = 0; i < 5; i++)
            {
                context.Execute();
                yield return null;
            }

            Assert.AreEqual(5, counter.Count);
        }

        [UnityTest]
        public IEnumerator ReactiveSystem_Triggers_On_Component_Add()
        {
            var context = new Context("PlayTest");
            var reactive = new PlayModeReactiveSystem(context);

            context.AddSystem(reactive);
            context.Initialize();

            var entity = context.CreateEntity();
            entity.AddComponent(new HealthComp { Max = 100, Current = 100 });

            yield return null;

            context.Execute();

            Assert.AreEqual(1, reactive.TriggeredCount);

            // 再次执行不应触发
            context.Execute();
            Assert.AreEqual(1, reactive.TriggeredCount);
        }

        [UnityTest]
        public IEnumerator ReactiveSystem_Triggers_Multiple_Times()
        {
            var context = new Context("PlayTest");
            var reactive = new PlayModeReactiveSystem(context);

            context.AddSystem(reactive);
            context.Initialize();

            for (int i = 0; i < 3; i++)
            {
                var entity = context.CreateEntity();
                entity.AddComponent(new HealthComp { Max = 100, Current = i });
                yield return null;
                context.Execute();
            }

            Assert.AreEqual(3, reactive.TriggeredCount);
        }

        private class LifecycleSystem : IInitializeSystem, IExecuteSystem, ICleanupSystem, ITearDownSystem
        {
            private readonly List<string> m_Lifecycle;

            public LifecycleSystem(List<string> lifecycle)
            {
                m_Lifecycle = lifecycle;
            }

            public void Initialize() => m_Lifecycle.Add("Init");
            public void Execute() => m_Lifecycle.Add("Execute");
            public void Cleanup() => m_Lifecycle.Add("Cleanup");
            public void TearDown() => m_Lifecycle.Add("TearDown");
        }

        private class CounterSystem : IExecuteSystem
        {
            public int Count { get; private set; }
            public void Execute() => Count++;
        }

        private class PlayModeReactiveSystem : ReactiveSystem
        {
            public int TriggeredCount { get; private set; }

            public PlayModeReactiveSystem(Context context) : base(context) { }

            protected override Matcher GetTrigger()
            {
                return Matcher.AllOf(ComponentTypeIndex<HealthComp>.Index);
            }

            protected override GroupEventType GetEventType()
            {
                return GroupEventType.OnEntityAdded;
            }

            protected override void Execute(List<Entity> entities)
            {
                TriggeredCount += entities.Count;
            }
        }
    }

    // ========== Play Mode 组件对象池测试 ==========

    public class PlayMode_ComponentPoolTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
            ComponentPool<TransformComp>.Clear();
            ComponentPool<TransformComp>.ResetStats();
        }

        [UnityTest]
        public IEnumerator ComponentPool_Reuse_In_PlayMode()
        {
            var comp1 = ComponentPool<TransformComp>.Get();
            comp1.Position = Vector3.one;

            ComponentPool<TransformComp>.Return(comp1);

            yield return null;

            var comp2 = ComponentPool<TransformComp>.Get();

            Assert.AreSame(comp1, comp2);
            Assert.AreEqual(1, ComponentPool<TransformComp>.ReusedCount);
        }

        [UnityTest]
        public IEnumerator ComponentPool_Integration_With_Entity()
        {
            var context = new Context("PlayTest");
            var entity = context.CreateEntity();

            var comp = ComponentPool<TransformComp>.Get(c =>
            {
                c.Position = new Vector3(10, 20, 30);
                c.Rotation = Quaternion.Euler(45, 0, 0);
            });

            entity.AddComponent(comp);

            yield return null;

            var retrieved = entity.GetComponent<TransformComp>();
            Assert.AreEqual(new Vector3(10, 20, 30), retrieved.Position);

            // 清理时回收组件
            var oldComp = entity.GetComponent<TransformComp>();
            entity.RemoveComponent<TransformComp>();
            ComponentPool<TransformComp>.Return(oldComp);

            Assert.AreEqual(1, ComponentPool<TransformComp>.Count);
        }
    }

    // ========== Play Mode EntityIndex 测试 ==========

    public class PlayMode_EntityIndexTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [UnityTest]
        public IEnumerator EntityIndex_Lookup_In_PlayMode()
        {
            var context = new Context("PlayTest");
            var index = new EntityIndex<HealthComp, int>(
                context,
                c => c.Current,
                Matcher.AllOf(ComponentTypeIndex<HealthComp>.Index)
            );

            var e1 = context.CreateEntity();
            e1.AddComponent(new HealthComp { Max = 100, Current = 50 });

            var e2 = context.CreateEntity();
            e2.AddComponent(new HealthComp { Max = 100, Current = 50 });

            var e3 = context.CreateEntity();
            e3.AddComponent(new HealthComp { Max = 100, Current = 80 });

            yield return null;

            var health50 = index.GetEntities(50);
            Assert.AreEqual(2, health50.Count);

            var health80 = index.GetEntities(80);
            Assert.AreEqual(1, health80.Count);

            var health999 = index.GetEntities(999);
            Assert.AreEqual(0, health999.Count);
        }

        [UnityTest]
        public IEnumerator EntityIndex_Auto_Update_On_Component_Change()
        {
            var context = new Context("PlayTest");
            var index = new EntityIndex<HealthComp, int>(
                context,
                c => c.Current,
                Matcher.AllOf(ComponentTypeIndex<HealthComp>.Index)
            );

            var entity = context.CreateEntity();
            entity.AddComponent(new HealthComp { Max = 100, Current = 50 });

            yield return null;

            Assert.AreEqual(1, index.GetEntities(50).Count);

            // 替换组件，索引应自动更新
            entity.ReplaceComponent(new HealthComp { Max = 100, Current = 75 });

            yield return null;

            Assert.AreEqual(0, index.GetEntities(50).Count);
            Assert.AreEqual(1, index.GetEntities(75).Count);
        }

        [UnityTest]
        public IEnumerator SingleEntityIndex_Lookup_In_PlayMode()
        {
            var context = new Context("PlayTest");
            var index = new SingleEntityIndex<HealthComp, int>(
                context,
                c => c.Current,
                Matcher.AllOf(ComponentTypeIndex<HealthComp>.Index)
            );

            var e1 = context.CreateEntity();
            e1.AddComponent(new HealthComp { Max = 100, Current = 42 });

            yield return null;

            var found = index.GetEntity(42);
            Assert.AreSame(e1, found);

            // 添加同键实体，应覆盖
            var e2 = context.CreateEntity();
            e2.AddComponent(new HealthComp { Max = 100, Current = 42 });

            yield return null;

            var overwritten = index.GetEntity(42);
            Assert.AreSame(e2, overwritten);
        }
    }

    // ========== Play Mode 压力测试 ==========

    public class PlayMode_StressTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [UnityTest]
        public IEnumerator Stress_Create_1000_Entities()
        {
            var context = new Context("Stress");
            const int count = 1000;

            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            for (int i = 0; i < count; i++)
            {
                var e = context.CreateEntity();
                e.AddComponent(new TransformComp { Position = new Vector3(i, i, i) });
                if (i % 2 == 0)
                    e.AddComponent(new VelocityComp { Value = Vector3.one });
                if (i % 3 == 0)
                    e.AddComponent(new HealthComp { Max = 100, Current = i });
            }

            sw.Stop();
            Debug.Log($"创建 {count} 个实体耗时: {sw.ElapsedMilliseconds} ms");

            yield return null;

            Assert.AreEqual(count, context.Count);

            var transformGroup = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<TransformComp>.Index));
            Assert.AreEqual(count, transformGroup.Count);

            context.Reset();
        }

        [UnityTest]
        public IEnumerator Stress_System_Execution_Performance()
        {
            var context = new Context("Stress");
            context.Systems.ProfilingEnabled = true;

            // 创建大量实体
            for (int i = 0; i < 500; i++)
            {
                var e = context.CreateEntity();
                e.AddComponent(new TransformComp { Position = Vector3.zero });
                e.AddComponent(new VelocityComp { Value = Vector3.one });
            }

            context.AddSystem(new MoveSystem(context));
            context.Initialize();

            yield return null;

            // 执行多帧
            for (int i = 0; i < 60; i++)
            {
                context.Execute();
                yield return null;
            }

            Assert.Greater(context.Systems.ProfileResults.Count, 0);

            Debug.Log($"60帧总执行耗时: {context.Systems.TotalExecuteTimeMs} ms");
            Debug.Log($"平均每帧: {context.Systems.TotalExecuteTimeMs / 60f:F3} ms");

            context.Reset();
        }

        [UnityTest]
        public IEnumerator Stress_Rapid_Create_Destroy()
        {
            var context = new Context("Stress");

            for (int round = 0; round < 10; round++)
            {
                var entities = new List<Entity>();
                for (int i = 0; i < 50; i++)
                {
                    var e = context.CreateEntity();
                    e.AddComponent(new TransformComp { Position = Vector3.one * i });
                    entities.Add(e);
                }

                yield return null;

                foreach (var e in entities)
                {
                    context.DestroyEntity(e);
                }

                yield return null;

                Assert.AreEqual(0, context.Count);
            }
        }

        [UnityTest]
        public IEnumerator Stress_Multiple_Contexts()
        {
            var gameContext = new Context("Game");
            var uiContext = new Context("UI");

            for (int i = 0; i < 100; i++)
            {
                var ge = gameContext.CreateEntity();
                ge.AddComponent(new TransformComp { Position = Vector3.one * i });

                var ue = uiContext.CreateEntity();
                ue.AddComponent(new HealthComp { Max = 100, Current = i });
            }

            yield return null;

            Assert.AreEqual(100, gameContext.Count);
            Assert.AreEqual(100, uiContext.Count);

            gameContext.Reset();
            uiContext.Reset();
        }

        private class MoveSystem : IExecuteSystem
        {
            private readonly Group m_Group;

            public MoveSystem(Context context)
            {
                m_Group = context.GetGroup(Matcher.AllOf(
                    ComponentTypeIndex<TransformComp>.Index,
                    ComponentTypeIndex<VelocityComp>.Index
                ));
            }

            public void Execute()
            {
                foreach (var entity in m_Group.GetEntities())
                {
                    var transform = entity.GetComponent<TransformComp>();
                    var velocity = entity.GetComponent<VelocityComp>();
                    transform.Position += velocity.Value * Time.deltaTime;
                }
            }
        }
    }

    // ========== Play Mode 边界情况测试 ==========

    public class PlayMode_EdgeCaseTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [UnityTest]
        public IEnumerator EdgeCase_Empty_Context_Execute()
        {
            var context = new Context("Empty");
            context.AddSystem(new EmptySystem());
            context.Initialize();

            yield return null;

            // 空上下文执行不应报错
            context.Execute();
            context.Cleanup();
            context.TearDown();

            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator EdgeCase_Entity_Add_Remove_Same_Component_Multiple_Times()
        {
            var context = new Context("Edge");
            var entity = context.CreateEntity();

            for (int i = 0; i < 10; i++)
            {
                entity.AddComponent(new TransformComp { Position = Vector3.one * i });
                yield return null;
                entity.RemoveComponent<TransformComp>();
                yield return null;
            }

            Assert.IsFalse(entity.HasComponent<TransformComp>());
            Assert.AreEqual(0, entity.GetComponentIndices().Length);

            context.DestroyEntity(entity);
        }

        [UnityTest]
        public IEnumerator EdgeCase_Group_SingleEntity_Throws_When_Empty()
        {
            var context = new Context("Edge");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<TransformComp>.Index));

            yield return null;

            Assert.Throws<System.Exception>(() => group.GetSingleEntity());
        }

        [UnityTest]
        public IEnumerator EdgeCase_Matcher_NoneOf_All_Excluded()
        {
            var context = new Context("Edge");
            var matcher = Matcher.AllOf(ComponentTypeIndex<TransformComp>.Index)
                .NoneOf(ComponentTypeIndex<VelocityComp>.Index);

            var e1 = context.CreateEntity();
            e1.AddComponent(new TransformComp());

            var e2 = context.CreateEntity();
            e2.AddComponent(new TransformComp());
            e2.AddComponent(new VelocityComp());

            yield return null;

            var group = context.GetGroup(matcher);
            Assert.AreEqual(1, group.Count);
            Assert.IsTrue(group.ContainsEntity(e1));
            Assert.IsFalse(group.ContainsEntity(e2));
        }

        [UnityTest]
        public IEnumerator EdgeCase_Collector_Deactivate_Reactivate()
        {
            var context = new Context("Edge");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<TransformComp>.Index));
            var collector = new Collector(group, GroupEventType.OnEntityAdded);

            collector.Deactivate();

            var entity = context.CreateEntity();
            entity.AddComponent(new TransformComp());

            yield return null;

            Assert.AreEqual(0, collector.Count);

            collector.Activate();

            var entity2 = context.CreateEntity();
            entity2.AddComponent(new TransformComp());

            yield return null;

            Assert.AreEqual(1, collector.Count);
        }

        private class EmptySystem : IExecuteSystem
        {
            public void Execute() { }
        }
    }
}
