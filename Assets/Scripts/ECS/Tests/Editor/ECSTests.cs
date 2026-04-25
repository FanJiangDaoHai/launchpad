using System.Collections.Generic;
using NUnit.Framework;

namespace EntitasLite.Tests
{
    // ========== 测试用组件 ==========

    public class CompA : IComponent { public int Value; }
    public class CompB : IComponent { public string Name; }
    public class CompC : IComponent { public bool Flag; }
    public class TagComp : IComponent { }

    // ========== Entity 测试 ==========

    public class EntityTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Entity_CreationIndex_IsUnique()
        {
            var e1 = new Entity("Test");
            var e2 = new Entity("Test");
            Assert.AreNotEqual(e1.CreationIndex, e2.CreationIndex);
        }

        [Test]
        public void Entity_IsEnabled_ByDefault()
        {
            var entity = new Entity("Test");
            Assert.IsTrue(entity.IsEnabled);
        }

        [Test]
        public void Entity_AddComponent_Generic()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA { Value = 42 });

            Assert.IsTrue(entity.HasComponent<CompA>());
            Assert.AreEqual(42, entity.GetComponent<CompA>().Value);
        }

        [Test]
        public void Entity_AddComponent_ReplacesExisting()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA { Value = 10 });
            entity.AddComponent(new CompA { Value = 20 });

            Assert.AreEqual(20, entity.GetComponent<CompA>().Value);
        }

        [Test]
        public void Entity_RemoveComponent_Generic()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA { Value = 1 });
            entity.RemoveComponent<CompA>();

            Assert.IsFalse(entity.HasComponent<CompA>());
        }

        [Test]
        public void Entity_ReplaceComponent_FiresEvent()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA { Value = 1 });

            bool fired = false;
            entity.OnComponentReplaced += (e, idx, prev, next) => fired = true;

            entity.ReplaceComponent(new CompA { Value = 99 });
            Assert.IsTrue(fired);
        }

        [Test]
        public void Entity_TryGetComponent_ReturnsFalse_WhenMissing()
        {
            var entity = new Entity("Test");
            bool result = entity.TryGetComponent<CompA>(out var comp);

            Assert.IsFalse(result);
            Assert.IsNull(comp);
        }

        [Test]
        public void Entity_TryGetComponent_ReturnsTrue_WhenPresent()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA { Value = 7 });

            bool result = entity.TryGetComponent<CompA>(out var comp);

            Assert.IsTrue(result);
            Assert.AreEqual(7, comp.Value);
        }

        [Test]
        public void Entity_GetComponentIndices_ReturnsCorrectIndices()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.AddComponent(new CompB());

            int[] indices = entity.GetComponentIndices();

            Assert.AreEqual(2, indices.Length);
            Assert.Contains(ComponentTypeIndex<CompA>.Index, indices);
            Assert.Contains(ComponentTypeIndex<CompB>.Index, indices);
        }

        [Test]
        public void Entity_Destroy_RemovesAllComponents()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.AddComponent(new CompB());

            entity.Destroy();

            Assert.IsFalse(entity.IsEnabled);
            Assert.AreEqual(0, entity.GetComponentIndices().Length);
        }
    }

    // ========== Matcher 测试 ==========

    public class MatcherTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Matcher_AllOf_MatchesEntityWithAllComponents()
        {
            var matcher = Matcher.AllOf(
                ComponentTypeIndex<CompA>.Index,
                ComponentTypeIndex<CompB>.Index
            );

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.AddComponent(new CompB());

            Assert.IsTrue(matcher.Matches(entity));
        }

        [Test]
        public void Matcher_AllOf_DoesNotMatch_MissingComponent()
        {
            var matcher = Matcher.AllOf(
                ComponentTypeIndex<CompA>.Index,
                ComponentTypeIndex<CompB>.Index
            );

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());

            Assert.IsFalse(matcher.Matches(entity));
        }

        [Test]
        public void Matcher_AnyOf_MatchesEntityWithOneComponent()
        {
            var matcher = Matcher.AnyOf(
                ComponentTypeIndex<CompA>.Index,
                ComponentTypeIndex<CompB>.Index
            );

            var entity = new Entity("Test");
            entity.AddComponent(new CompB());

            Assert.IsTrue(matcher.Matches(entity));
        }

        [Test]
        public void Matcher_NoneOf_ExcludesEntityWithForbiddenComponent()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
                .NoneOf(ComponentTypeIndex<CompB>.Index);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.AddComponent(new CompB());

            Assert.IsFalse(matcher.Matches(entity));
        }

        [Test]
        public void Matcher_NoneOf_IncludesEntityWithoutForbiddenComponent()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
                .NoneOf(ComponentTypeIndex<CompB>.Index);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());

            Assert.IsTrue(matcher.Matches(entity));
        }

        [Test]
        public void Matcher_Equality_SameMatchersAreEqual()
        {
            var m1 = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var m2 = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);

            Assert.AreEqual(m1, m2);
            Assert.AreEqual(m1.GetHashCode(), m2.GetHashCode());
        }

        [Test]
        public void Matcher_Equality_DifferentMatchersAreNotEqual()
        {
            var m1 = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var m2 = Matcher.AllOf(ComponentTypeIndex<CompB>.Index);

            Assert.AreNotEqual(m1, m2);
        }
    }

    // ========== Group 测试 ==========

    public class GroupTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Group_CachesMatchingEntities()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());

            group.HandleEntitySilentlyForCollector(entity);

            Assert.AreEqual(1, group.Count);
            Assert.AreSame(entity, group.GetEntities()[0]);
        }

        [Test]
        public void Group_OnEntityAdded_FiresEvent()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            Entity addedEntity = null;
            group.OnEntityAdded += e => addedEntity = e;

            var entity = new Entity("Test");
            group.HandleEntity(entity);
            entity.AddComponent(new CompA());
            group.HandleEntity(entity);

            Assert.AreSame(entity, addedEntity);
        }

        [Test]
        public void Group_OnEntityRemoved_FiresEvent_WhenComponentRemoved()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            group.HandleEntity(entity);

            Entity removedEntity = null;
            group.OnEntityRemoved += e => removedEntity = e;

            entity.RemoveComponent<CompA>();
            group.HandleEntitySilently(entity);

            Assert.AreSame(entity, removedEntity);
        }

        [Test]
        public void Group_GetSingleEntity_ReturnsEntity()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            group.HandleEntitySilentlyForCollector(entity);

            Assert.AreSame(entity, group.GetSingleEntity());
        }

        [Test]
        public void Group_GetSingleEntity_Throws_WhenMultiple()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            group.HandleEntitySilentlyForCollector(new Entity("Test").AddComponent(new CompA()));
            group.HandleEntitySilentlyForCollector(new Entity("Test").AddComponent(new CompA()));

            Assert.Throws<System.Exception>(() => group.GetSingleEntity());
        }

        [Test]
        public void Group_ContainsEntity_ReturnsTrue()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            group.HandleEntitySilentlyForCollector(entity);

            Assert.IsTrue(group.ContainsEntity(entity));
        }
    }

    // ========== Context 测试 ==========

    public class ContextTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Context_CreateEntity_ReturnsValidEntity()
        {
            var context = new Context("Test");
            var entity = context.CreateEntity();

            Assert.IsNotNull(entity);
            Assert.IsTrue(entity.IsEnabled);
            Assert.AreEqual("Test", entity.ContextName);
        }

        [Test]
        public void Context_HasEntity_ReturnsTrue()
        {
            var context = new Context("Test");
            var entity = context.CreateEntity();

            Assert.IsTrue(context.HasEntity(entity));
        }

        [Test]
        public void Context_DestroyEntity_RemovesEntity()
        {
            var context = new Context("Test");
            var entity = context.CreateEntity();
            context.DestroyEntity(entity);

            Assert.IsFalse(context.HasEntity(entity));
            Assert.IsFalse(entity.IsEnabled);
        }

        [Test]
        public void Context_GetGroup_ReturnsSameGroupForSameMatcher()
        {
            var context = new Context("Test");
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);

            var group1 = context.GetGroup(matcher);
            var group2 = context.GetGroup(matcher);

            Assert.AreSame(group1, group2);
        }

        [Test]
        public void Context_GetGroup_AutoPopulatesExistingEntities()
        {
            var context = new Context("Test");
            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());

            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = context.GetGroup(matcher);

            Assert.AreEqual(1, group.Count);
        }

        [Test]
        public void Context_EntityPool_ReusesDestroyedEntities()
        {
            var context = new Context("Test");
            var entity = context.CreateEntity();
            int oldIndex = entity.CreationIndex;

            context.DestroyEntity(entity);
            var newEntity = context.CreateEntity();

            // 对象池回收后，引用地址相同，但 CreationIndex 重新分配
            Assert.IsTrue(newEntity.IsEnabled);
            Assert.AreNotEqual(oldIndex, newEntity.CreationIndex);
        }

        [Test]
        public void Context_AddSystem_AndInitialize()
        {
            var context = new Context("Test");
            bool initialized = false;

            context.AddSystem(new TestInitSystem(() => initialized = true));
            context.Initialize();

            Assert.IsTrue(initialized);
        }

        [Test]
        public void Context_Execute_RunsExecuteSystems()
        {
            var context = new Context("Test");
            int count = 0;

            context.AddSystem(new TestExecSystem(() => count++));
            context.Execute();
            context.Execute();

            Assert.AreEqual(2, count);
        }

        // 测试用的简单系统
        private class TestInitSystem : IInitializeSystem
        {
            private readonly System.Action m_Action;
            public TestInitSystem(System.Action action) { m_Action = action; }
            public void Initialize() => m_Action();
        }

        private class TestExecSystem : IExecuteSystem
        {
            private readonly System.Action m_Action;
            public TestExecSystem(System.Action action) { m_Action = action; }
            public void Execute() => m_Action();
        }
    }

    // ========== Collector 测试 ==========

    public class CollectorTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Collector_CollectsAddedEntities()
        {
            var context = new Context("Test");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            var collector = new Collector(group, GroupEventType.OnEntityAdded);

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());

            Assert.AreEqual(1, collector.Count);
            Assert.Contains(entity, collector.GetCollectedEntities());
        }

        [Test]
        public void Collector_ClearCollectedEntities_RemovesAll()
        {
            var context = new Context("Test");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            var collector = new Collector(group, GroupEventType.OnEntityAdded);

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());

            collector.ClearCollectedEntities();

            Assert.AreEqual(0, collector.Count);
        }

        [Test]
        public void Collector_Deactivate_StopsCollecting()
        {
            var context = new Context("Test");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            var collector = new Collector(group, GroupEventType.OnEntityAdded);

            collector.Deactivate();

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());

            Assert.AreEqual(0, collector.Count);
        }
    }

    // ========== ReactiveSystem 测试 ==========

    public class ReactiveSystemTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void ReactiveSystem_Executes_WhenEntityMatchesTrigger()
        {
            var context = new Context("Test");
            var executedEntities = new List<Entity>();

            var reactive = new TestReactiveSystem(context, executedEntities);
            context.AddSystem(reactive);
            context.Initialize();

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());

            context.Execute();

            Assert.AreEqual(1, executedEntities.Count);
            Assert.AreSame(entity, executedEntities[0]);
        }

        [Test]
        public void ReactiveSystem_DoesNotExecute_WhenNoChanges()
        {
            var context = new Context("Test");
            var executedEntities = new List<Entity>();

            var reactive = new TestReactiveSystem(context, executedEntities);
            context.AddSystem(reactive);
            context.Initialize();

            context.Execute();

            Assert.AreEqual(0, executedEntities.Count);
        }

        [Test]
        public void ReactiveSystem_ClearsCollectorAfterExecute()
        {
            var context = new Context("Test");
            var executedEntities = new List<Entity>();

            var reactive = new TestReactiveSystem(context, executedEntities);
            context.AddSystem(reactive);
            context.Initialize();

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());

            context.Execute();
            context.Execute(); // 第二次不应再触发

            Assert.AreEqual(1, executedEntities.Count);
        }

        private class TestReactiveSystem : ReactiveSystem
        {
            private readonly List<Entity> m_Target;

            public TestReactiveSystem(Context context, List<Entity> target) : base(context)
            {
                m_Target = target;
            }

            protected override Matcher GetTrigger()
            {
                return Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            }

            protected override GroupEventType GetEventType()
            {
                return GroupEventType.OnEntityAdded;
            }

            protected override void Execute(List<Entity> entities)
            {
                m_Target.AddRange(entities);
            }
        }
    }

    // ========== Entity 边界测试 ==========

    public class EntityEdgeCaseTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Entity_AddComponent_To_Destroyed_Throws()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.Destroy();

            Assert.Throws<System.Exception>(() => entity.AddComponent(new CompB()));
        }

        [Test]
        public void Entity_RemoveComponent_From_Destroyed_Throws()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.Destroy();

            Assert.Throws<System.Exception>(() => entity.RemoveComponent<CompA>());
        }

        [Test]
        public void Entity_GetComponent_Missing_Throws()
        {
            var entity = new Entity("Test");

            Assert.Throws<System.Exception>(() => entity.GetComponent<CompA>());
        }

        [Test]
        public void Entity_ReplaceComponent_SameReference_NoEvent()
        {
            var entity = new Entity("Test");
            var comp = new CompA { Value = 1 };
            entity.AddComponent(comp);

            bool fired = false;
            entity.OnComponentReplaced += (e, idx, prev, next) => fired = true;

            entity.ReplaceComponent(ComponentTypeIndex<CompA>.Index, comp);

            Assert.IsFalse(fired);
        }

        [Test]
        public void Entity_HasComponents_AllRequired()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            entity.AddComponent(new CompB());

            Assert.IsTrue(entity.HasComponents(new[] { ComponentTypeIndex<CompA>.Index, ComponentTypeIndex<CompB>.Index }));
            Assert.IsFalse(entity.HasComponents(new[] { ComponentTypeIndex<CompA>.Index, ComponentTypeIndex<CompC>.Index }));
        }

        [Test]
        public void Entity_HasAnyComponent_OneMatch()
        {
            var entity = new Entity("Test");
            entity.AddComponent(new CompA());

            Assert.IsTrue(entity.HasAnyComponent(new[] { ComponentTypeIndex<CompA>.Index, ComponentTypeIndex<CompB>.Index }));
            Assert.IsFalse(entity.HasAnyComponent(new[] { ComponentTypeIndex<CompB>.Index, ComponentTypeIndex<CompC>.Index }));
        }

        [Test]
        public void Entity_ToString_Contains_Info()
        {
            var entity = new Entity("Game");
            entity.AddComponent(new CompA { Value = 42 });

            var str = entity.ToString();

            Assert.IsTrue(str.Contains("Entity_"));
            Assert.IsTrue(str.Contains("Game"));
        }
    }

    // ========== Matcher 边界测试 ==========

    public class MatcherEdgeCaseTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Matcher_AllOf_Empty_Throws()
        {
            Assert.Throws<System.Exception>(() => Matcher.AllOf());
        }

        [Test]
        public void Matcher_AnyOf_Empty_Throws()
        {
            Assert.Throws<System.Exception>(() => Matcher.AnyOf());
        }

        [Test]
        public void Matcher_NoneOf_Only_Excludes()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
                .NoneOf(ComponentTypeIndex<CompB>.Index, ComponentTypeIndex<CompC>.Index);

            var e1 = new Entity("Test");
            e1.AddComponent(new CompA());

            var e2 = new Entity("Test");
            e2.AddComponent(new CompA());
            e2.AddComponent(new CompB());

            Assert.IsTrue(matcher.Matches(e1));
            Assert.IsFalse(matcher.Matches(e2));
        }

        [Test]
        public void Matcher_HashCode_Consistent()
        {
            var m1 = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var m2 = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);

            Assert.AreEqual(m1.GetHashCode(), m2.GetHashCode());
        }
    }

    // ========== Group 边界测试 ==========

    public class GroupEdgeCaseTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Group_TryGetSingleEntity_ReturnsFalse_WhenEmpty()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            bool result = group.TryGetSingleEntity(out var entity);

            Assert.IsFalse(result);
            Assert.IsNull(entity);
        }

        [Test]
        public void Group_TryGetSingleEntity_ReturnsTrue_WhenOne()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var entity = new Entity("Test");
            entity.AddComponent(new CompA());
            group.HandleEntitySilentlyForCollector(entity);

            bool result = group.TryGetSingleEntity(out var found);

            Assert.IsTrue(result);
            Assert.AreSame(entity, found);
        }

        [Test]
        public void Group_GetEntities_Returns_Cached_Array()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var e1 = new Entity("Test");
            e1.AddComponent(new CompA());
            group.HandleEntitySilentlyForCollector(e1);

            var array1 = group.GetEntities();
            var array2 = group.GetEntities();

            Assert.AreSame(array1, array2);
        }

        [Test]
        public void Group_GetEntities_List_Clears_Before_Fill()
        {
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);
            var group = new Group(matcher);

            var list = new List<Entity> { null, null, null };
            group.GetEntities(list);

            Assert.AreEqual(0, list.Count);
        }
    }

    // ========== Context 边界测试 ==========

    public class ContextEdgeCaseTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Context_DestroyEntity_NotInContext_Throws()
        {
            var context1 = new Context("C1");
            var context2 = new Context("C2");

            var entity = context1.CreateEntity();

            Assert.Throws<System.Exception>(() => context2.DestroyEntity(entity));
        }

        [Test]
        public void Context_GetEntities_Returns_Copy()
        {
            var context = new Context("Test");
            context.CreateEntity().AddComponent(new CompA());

            var array1 = context.GetEntities();
            var array2 = context.GetEntities();

            Assert.AreNotSame(array1, array2);
        }

        [Test]
        public void Context_ClearGroups_Removes_All()
        {
            var context = new Context("Test");
            var group1 = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            var group2 = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompB>.Index));

            context.ClearGroups();

            // 再次获取应该是新实例
            var group1New = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            Assert.AreNotSame(group1, group1New);
        }

        [Test]
        public void Context_Multiple_Groups_Same_Matcher_Are_Same()
        {
            var context = new Context("Test");
            var matcher = Matcher.AllOf(ComponentTypeIndex<CompA>.Index);

            var g1 = context.GetGroup(matcher);
            var g2 = context.GetGroup(matcher);
            var g3 = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));

            Assert.AreSame(g1, g2);
            Assert.AreSame(g1, g3);
        }
    }

    // ========== Collector 边界测试 ==========

    public class CollectorEdgeCaseTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Collector_Activate_After_Deactivate_Resumes()
        {
            var context = new Context("Test");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            var collector = new Collector(group, GroupEventType.OnEntityAdded);

            collector.Deactivate();
            var e1 = context.CreateEntity();
            e1.AddComponent(new CompA());
            Assert.AreEqual(0, collector.Count);

            collector.Activate();
            var e2 = context.CreateEntity();
            e2.AddComponent(new CompA());
            Assert.AreEqual(1, collector.Count);
        }

        [Test]
        public void Collector_Collects_SameEntity_Once()
        {
            var context = new Context("Test");
            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            var collector = new Collector(group, GroupEventType.OnEntityAdded);

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA());
            // 再次添加不同组件不应重复收集（因为OnEntityAdded只触发一次）
            entity.AddComponent(new CompB());

            Assert.AreEqual(1, collector.Count);
        }
    }

    // ========== 组件对象池测试 ==========

    public class ComponentPoolTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentPool<CompA>.Clear();
            ComponentPool<CompA>.ResetStats();
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void ComponentPool_Get_CreatesNew_WhenEmpty()
        {
            var comp = ComponentPool<CompA>.Get();

            Assert.IsNotNull(comp);
            Assert.AreEqual(1, ComponentPool<CompA>.CreatedCount);
            Assert.AreEqual(0, ComponentPool<CompA>.ReusedCount);
        }

        [Test]
        public void ComponentPool_Get_ReusesReturned()
        {
            var comp = ComponentPool<CompA>.Get();
            ComponentPool<CompA>.Return(comp);
            var reused = ComponentPool<CompA>.Get();

            Assert.AreSame(comp, reused);
            Assert.AreEqual(1, ComponentPool<CompA>.CreatedCount);
            Assert.AreEqual(1, ComponentPool<CompA>.ReusedCount);
        }

        [Test]
        public void ComponentPool_Get_WithInit()
        {
            var comp = ComponentPool<CompA>.Get(c => c.Value = 99);

            Assert.AreEqual(99, comp.Value);
        }

        [Test]
        public void ComponentPool_Clear_EmptiesPool()
        {
            ComponentPool<CompA>.Return(new CompA());
            ComponentPool<CompA>.Clear();

            Assert.AreEqual(0, ComponentPool<CompA>.Count);
        }
    }

    // ========== 系统耗时分析测试 ==========

    public class SystemProfileTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Systems_Profiling_Enabled_RecordsTimes()
        {
            var systems = new Systems();
            systems.Add(new TestExecSystem(() => { }));
            systems.ProfilingEnabled = true;

            systems.Execute();

            Assert.AreEqual(1, systems.ProfileResults.Count);
            Assert.AreEqual("TestExecSystem", systems.ProfileResults[0].Name);
        }

        [Test]
        public void Systems_Profiling_Disabled_DoesNotRecord()
        {
            var systems = new Systems();
            systems.Add(new TestExecSystem(() => { }));
            systems.ProfilingEnabled = false;

            systems.Execute();

            Assert.AreEqual(0, systems.ProfileResults.Count);
        }

        [Test]
        public void Systems_Profile_SortsByTime()
        {
            var systems = new Systems();
            systems.Add(new TestExecSystem(() => System.Threading.Thread.Sleep(5)));
            systems.Add(new TestExecSystem(() => { }));
            systems.ProfilingEnabled = true;

            systems.Execute();

            var results = systems.ProfileResults;
            Assert.AreEqual(2, results.Count);
            // 第一个应该耗时更长（因为睡了 5ms）
            Assert.GreaterOrEqual(results[0].ElapsedMilliseconds, results[1].ElapsedMilliseconds);
        }

        private class TestExecSystem : IExecuteSystem
        {
            private readonly System.Action m_Action;
            public TestExecSystem(System.Action action) { m_Action = action; }
            public void Execute() => m_Action();
        }
    }

    // ========== EntityIndex 测试 ==========

    public class EntityIndexTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void EntityIndex_GetEntities_ByKey()
        {
            var context = new Context("Test");
            var index = new EntityIndex<CompA, int>(
                context,
                c => c.Value,
                Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
            );

            var e1 = context.CreateEntity();
            e1.AddComponent(new CompA { Value = 42 });

            var e2 = context.CreateEntity();
            e2.AddComponent(new CompA { Value = 42 });

            var e3 = context.CreateEntity();
            e3.AddComponent(new CompA { Value = 99 });

            var entities = index.GetEntities(42);

            Assert.AreEqual(2, entities.Count);
            Assert.IsTrue(entities.Contains(e1));
            Assert.IsTrue(entities.Contains(e2));
        }

        [Test]
        public void EntityIndex_RemovesEntity_WhenComponentRemoved()
        {
            var context = new Context("Test");
            var index = new EntityIndex<CompA, int>(
                context,
                c => c.Value,
                Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
            );

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA { Value = 42 });
            entity.RemoveComponent<CompA>();

            var entities = index.GetEntities(42);
            Assert.AreEqual(0, entities.Count);
        }

        [Test]
        public void SingleEntityIndex_GetEntity_ReturnsSingle()
        {
            var context = new Context("Test");
            var index = new SingleEntityIndex<CompA, int>(
                context,
                c => c.Value,
                Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
            );

            var entity = context.CreateEntity();
            entity.AddComponent(new CompA { Value = 42 });

            Assert.AreSame(entity, index.GetEntity(42));
        }

        [Test]
        public void SingleEntityIndex_OverwritesDuplicateKey()
        {
            var context = new Context("Test");
            var index = new SingleEntityIndex<CompA, int>(
                context,
                c => c.Value,
                Matcher.AllOf(ComponentTypeIndex<CompA>.Index)
            );

            var e1 = context.CreateEntity();
            e1.AddComponent(new CompA { Value = 42 });

            var e2 = context.CreateEntity();
            e2.AddComponent(new CompA { Value = 42 });

            // 后添加的覆盖先添加的
            Assert.AreSame(e2, index.GetEntity(42));
        }
    }

    // ========== 压力与边界测试 ==========

    public class StressTests
    {
        [SetUp]
        public void Setup()
        {
            ComponentTypeIndex.Reset();
        }

        [Test]
        public void Stress_LargeEntityCount()
        {
            var context = new Context("Stress");
            const int count = 1000;

            for (int i = 0; i < count; i++)
            {
                var entity = context.CreateEntity();
                entity.AddComponent(new CompA { Value = i });
                if (i % 2 == 0)
                    entity.AddComponent(new CompB { Name = $"Entity_{i}" });
            }

            Assert.AreEqual(count, context.Count);

            var group = context.GetGroup(Matcher.AllOf(ComponentTypeIndex<CompA>.Index));
            Assert.AreEqual(count, group.Count);

            var groupAB = context.GetGroup(Matcher.AllOf(
                ComponentTypeIndex<CompA>.Index,
                ComponentTypeIndex<CompB>.Index
            ));
            Assert.AreEqual(count / 2, groupAB.Count);
        }

        [Test]
        public void Stress_MassDestroyAndReuse()
        {
            var context = new Context("Stress");

            // 创建并销毁大量实体，测试对象池
            for (int round = 0; round < 10; round++)
            {
                var entities = new List<Entity>();
                for (int i = 0; i < 100; i++)
                {
                    var e = context.CreateEntity();
                    e.AddComponent(new CompA { Value = i });
                    entities.Add(e);
                }

                foreach (var e in entities)
                {
                    context.DestroyEntity(e);
                }
            }

            // 所有实体都应被销毁
            Assert.AreEqual(0, context.Count);
        }

        [Test]
        public void Stress_SystemProfiling_ManySystems()
        {
            var systems = new Systems();
            for (int i = 0; i < 20; i++)
            {
                systems.Add(new TestExecSystem(() => { }));
            }
            systems.ProfilingEnabled = true;

            systems.Execute();

            Assert.AreEqual(20, systems.ProfileResults.Count);
        }

        private class TestExecSystem : IExecuteSystem
        {
            private readonly System.Action m_Action;
            public TestExecSystem(System.Action action) { m_Action = action; }
            public void Execute() => m_Action();
        }
    }
}
