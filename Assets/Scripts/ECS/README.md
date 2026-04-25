# EntitasLite - 轻量级ECS框架

参照 [Entitas](https://github.com/sschmid/Entitas) 设计的轻量级ECS（Entity-Component-System）框架，适用于Unity项目。

## 核心概念

| 概念 | 说明 |
|------|------|
| **Component** | 纯数据容器，实现 `IComponent` 接口 |
| **Entity** | 组件的容器，通过唯一ID标识 |
| **Context** | 实体的工厂和管理器，内置系统容器，支持对象池 |
| **Matcher** | 匹配器，用于筛选具有特定组件组合的实体 |
| **Group** | 实体组，自动缓存匹配Matcher的实体 |
| **Collector** | 收集器，监听Group事件并收集变化实体 |
| **System** | 处理逻辑的执行单元 |
| **Systems** | 系统容器（已内置于Context，通常无需直接操作） |
| **ComponentPool** | 组件对象池，减少GC分配 |
| **EntityIndex** | 按组件字段值快速查找实体 |

## 快速开始

### 1. 定义组件

```csharp
using EntitasLite;
using UnityEngine;

public class PositionComponent : IComponent
{
    public Vector3 Value;
}

public class VelocityComponent : IComponent
{
    public Vector3 Value;
}

// 标记组件（空组件）
public class PlayerComponent : IComponent { }
```

### 2. 创建系统

```csharp
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
```

### 3. 初始化和运行

```csharp
public class GameController : MonoBehaviour
{
    private Context m_Context;

    void Start()
    {
        // 创建上下文（无需预先指定组件总数，组件类型自动发现）
        m_Context = new Context("Game");

        // 链式添加系统
        m_Context
            .AddSystem(new MoveSystem(m_Context))
            .AddSystem(new SomeOtherSystem(m_Context));

        // 初始化
        m_Context.Initialize();
    }

    void Update()
    {
        m_Context.Execute();
    }

    void LateUpdate()
    {
        m_Context.Cleanup();
    }

    void OnDestroy()
    {
        m_Context.TearDown();
        m_Context.Reset();
    }
}
```

## Matcher用法

```csharp
// 必须包含所有指定组件
Matcher.AllOf(ComponentTypeIndex<A>.Index, ComponentTypeIndex<B>.Index);

// 至少包含其中一个
Matcher.AnyOf(ComponentTypeIndex<A>.Index, ComponentTypeIndex<B>.Index);

// 链式组合：包含A和B，但不包含C
Matcher.AllOf(ComponentTypeIndex<A>.Index, ComponentTypeIndex<B>.Index)
       .NoneOf(ComponentTypeIndex<C>.Index);
```

## 系统类型

| 接口 | 执行时机 |
|------|---------|
| `IInitializeSystem` | 调用 `Context.Initialize()` 时执行一次 |
| `IExecuteSystem` | 每帧调用 `Context.Execute()` 时执行 |
| `ICleanupSystem` | 每帧调用 `Context.Cleanup()` 时执行（通常用于销毁等后处理） |
| `ITearDownSystem` | 调用 `Context.TearDown()` 时执行（应用退出时） |
| `ReactiveSystem` | 继承此类，自动管理Collector，响应实体变化事件 |

## 响应式系统（ReactiveSystem）

响应式系统只在实体满足特定条件发生变化时执行，而不是每帧遍历所有实体。适合"只在触发时执行一次逻辑"的场景（如播放音效、触发特效、统计事件等）。

### 使用方式

继承 `ReactiveSystem`，指定触发 Matcher 和事件类型：

```csharp
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
            Debug.Log($"实体 {entity.CreationIndex} 生命值: {health.Current}/{health.Max}");
        }
    }
}
```

添加方式与普通系统一样：

```csharp
m_Context.AddSystem(new HealthAddedSystem(m_Context));
```

## 组件对象池（ComponentPool）

对于频繁创建和销毁的组件，使用对象池减少GC压力：

```csharp
// 从池中获取组件（池空则自动创建）
var pos = ComponentPool<PositionComponent>.Get();
pos.Value = new Vector3(1, 2, 3);
entity.ReplaceComponent(pos);

// 旧组件回收到池中
var oldPos = entity.GetComponent<PositionComponent>();
ComponentPool<PositionComponent>.Return(oldPos);

// 获取并初始化（一行代码）
var vel = ComponentPool<VelocityComponent>.Get(c => c.Value = Vector3.right);
entity.AddComponent(vel);
```

每个组件类型有独立的池，自动统计创建/复用次数：

```csharp
Debug.Log($"PositionComponent 池大小: {ComponentPool<PositionComponent>.Count}");
Debug.Log($"累计创建: {ComponentPool<PositionComponent>.CreatedCount}");
Debug.Log($"累计复用: {ComponentPool<PositionComponent>.ReusedCount}");
```

## 实体索引（EntityIndex）

按组件字段值快速查找实体，避免每帧遍历所有实体：

```csharp
// 多对多索引：一个键可对应多个实体
var index = new EntityIndex<PlayerComponent, int>(
    context,
    c => c.PlayerId,           // 按 PlayerId 字段索引
    Matcher.AllOf(ComponentTypeIndex<PlayerComponent>.Index)
);

// 查找玩家ID为42的所有实体
var players = index.GetEntities(42);

// 单键索引：一个键只对应一个实体（后添加的覆盖先添加的）
var singleIndex = new SingleEntityIndex<PositionComponent, int>(
    context,
    c => (int)c.Value.x,       // 按X坐标索引
    Matcher.AllOf(ComponentTypeIndex<PositionComponent>.Index)
);

var entity = singleIndex.GetEntity(10);
```

## 系统执行耗时分析

开启耗时分析，定位性能瓶颈：

```csharp
// 开启分析
m_Context.Systems.ProfilingEnabled = true;

// 执行后查看结果
m_Context.Execute();

foreach (var info in m_Context.Systems.ProfileResults)
{
    Debug.Log($"{info.Name}: {info.ElapsedMilliseconds} ms");
}
Debug.Log($"总耗时: {m_Context.Systems.TotalExecuteTimeMs} ms");
```

## Unity Editor 调试窗口

菜单栏打开：`EntitasLite → ECS Debugger`

运行时功能：
- 查看所有 Context 及其实体数量
- 展开实体查看组件和字段值
- 查看系统执行耗时柱状图
- 手动销毁实体

## 文件结构

```
Assets/Scripts/ECS/
├── README.md                        # 本文档
├── Runtime/                         # 运行时核心代码（EntitasLite.Runtime）
│   ├── EntitasLite.Runtime.asmdef   # Runtime 程序集定义
│   ├── Interfaces.cs                # 核心接口定义
│   ├── Entity.cs                    # 实体
│   ├── ComponentTypeIndex.cs        # 组件索引管理
│   ├── ComponentPool.cs             # 组件对象池
│   ├── Matcher.cs                   # 匹配器
│   ├── Group.cs                     # 实体组
│   ├── Context.cs                   # 上下文（已整合Systems）
│   ├── ContextRegistry.cs           # Context全局注册表（Editor调试用）
│   ├── Collector.cs                 # 收集器
│   ├── Systems.cs                   # 系统容器（支持耗时分析）
│   ├── ReactiveSystem.cs            # 响应式系统基类
│   ├── EntityIndex.cs               # 实体索引查找
│   └── Example/                     # 示例代码
│       ├── GameComponents.cs
│       ├── GameSystems.cs
│       └── GameController.cs
├── Editor/                          # 编辑器扩展（EntitasLite.Editor）
│   ├── EntitasLite.Editor.asmdef    # Editor 程序集定义
│   └── ECSEditorWindow.cs           # Unity Editor调试窗口
└── Tests/
    ├── Editor/                        # Editor Mode 测试（EntitasLite.Tests）
    │   ├── EntitasLite.Tests.asmdef
    │   └── ECSTests.cs
    └── PlayMode/                      # Play Mode 测试（EntitasLite.PlayModeTests）
        ├── EntitasLite.PlayModeTests.asmdef
        └── ECSPlayModeTests.cs
```

## 程序集说明

| 程序集 | 文件夹 | 用途 | 引用关系 |
|--------|--------|------|---------|
| `EntitasLite.Runtime` | `Runtime/` | 运行时核心代码 | 无 |
| `EntitasLite.Editor` | `Editor/` | Unity Editor 扩展 | 引用 Runtime |
| `EntitasLite.Tests` | `Tests/Editor/` | Editor Mode 单元测试 | 引用 Runtime + NUnit |
| `EntitasLite.PlayModeTests` | `Tests/PlayMode/` | Play Mode 集成测试 | 引用 Runtime + TestRunner + NUnit |

## 设计特点

1. **Context整合Systems** - 只需操作Context一个对象，降低业务开发门槛
2. **零GC分配** - Group.GetEntities() 返回缓存数组，支持填充List避免GC
3. **实体对象池** - Context自动回收销毁的实体，减少内存分配
4. **组件对象池** - `ComponentPool<T>` 减少频繁创建/销毁组件的GC压力
5. **事件驱动** - 组件增删改自动触发Group更新和Collector收集
6. **类型安全** - 泛型API `GetComponent<T>()` 保证类型安全
7. **实体索引** - `EntityIndex` 按字段值O(1)查找实体
8. **性能分析** - 内置系统执行耗时统计，方便定位瓶颈
9. **Editor调试** - 运行时可视化查看实体、组件、系统状态
10. **完整测试覆盖** - Editor Mode + Play Mode 双模式测试，核心 API 全覆盖
11. **性能优化** - 数组+位图组件存储、SwapBack实体移除、静态空集合缓存、分支合并等
12. **轻量级** - 核心代码约1200行，无外部依赖

## 与原版Entitas的区别

| 特性 | Entitas | EntitasLite |
|------|---------|-------------|
| 代码生成 | 需要代码生成工具 | 纯运行时，无代码生成 |
| 组件索引 | 编译时常量 | 运行时自动分配 |
| 系统管理 | 独立的Systems对象 | 已整合到Context |
| 组件对象池 | 代码生成自动处理 | `ComponentPool<T>` 显式使用 |
| 实体索引 | 完善支持 | `EntityIndex<TComponent,TKey>` |
| 性能分析 | 需外部工具 | 内置耗时统计 |
| Editor调试 | 需外部工具 | 内置调试窗口 |
| 多上下文 | 完善支持 | 基础支持 |
| 序列化 | 支持 | 需自行实现 |
| 组件存储 | Dictionary | 数组+位图（O(1)无哈希开销） |
| 代码量 | 庞大 | ~1200行核心代码 |
