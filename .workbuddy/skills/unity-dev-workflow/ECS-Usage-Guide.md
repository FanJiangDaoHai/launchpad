# ECS 框架使用流程

> 用本项目的 ECS 框架开发游戏逻辑的完整步骤。

---

## 流程概览

```
定义组件 → 写系统 → 创建 Context → 注册系统 → 初始化 → 运行
```

---

## Step 1: 定义组件（Component）

组件是纯数据，实现 `IComponent` 空接口即可。

```csharp
// ✅ 正确
public class PositionComponent : IComponent
{
    public float X, Y, Z;
}

public class VelocityComponent : IComponent
{
    public float X, Y, Z;
}

public class PlayerComponent : IComponent { }  // 标记组件，无数据
```

**规则**：
- 组件名以 `Component` 结尾
- 只放数据，不放逻辑
- 标记组件可以空实现

---

## Step 2: 写系统（System）

系统放逻辑，实现 `IInitializeSystem`、`IExecuteSystem` 等接口。

```csharp
public class MoveSystem : IExecuteSystem
{
    private readonly Context m_Context;
    private readonly Group m_MoveGroup;

    public MoveSystem(Context context)
    {
        m_Context = context;
        m_MoveGroup = context.GetGroup(
            Matcher.AllOf(
                ComponentTypeIndex<PositionComponent>.Index,
                ComponentTypeIndex<VelocityComponent>.Index
            )
        );
    }

    public void Execute()
    {
        foreach (var entity in m_MoveGroup.GetEntities())
        {
            var pos = entity.GetComponent<PositionComponent>();
            var vel = entity.GetComponent<VelocityComponent>();

            pos.X += vel.X * Time.deltaTime;
            pos.Y += vel.Y * Time.deltaTime;
        }
    }
}
```

**系统类型**：

| 接口 | 调用时机 | 用途 |
|------|---------|------|
| `IInitializeSystem` | `context.Initialize()` | 初始化，创建初始实体 |
| `IExecuteSystem` | `context.Execute()` | 每帧执行，放主逻辑 |
| `ICleanupSystem` | `context.Cleanup()` | 每帧清理，如销毁标记实体 |
| `ITearDownSystem` | `context.TearDown()` | 游戏结束/场景切换时 |

---

## Step 3: 创建 Context 并注册系统

在 `GameController`（或入口类）中：

```csharp
public class GameController : MonoBehaviour
{
    private Context m_Context;

    void Start()
    {
        // 创建上下文（无需指定组件总数）
        m_Context = new Context("Game");

        // 链式注册系统
        m_Context
            .AddSystem(new GameInitSystem(m_Context))   // 初始化
            .AddSystem(new MoveSystem(m_Context))       // 移动
            .AddSystem(new DestroySystem(m_Context))    // 销毁
            .AddSystem(new LogSystem(m_Context));       // 日志

        // 初始化所有系统
        m_Context.Initialize();
    }

    void Update()
    {
        m_Context.Execute();
        m_Context.Cleanup();
    }

    void OnDestroy()
    {
        m_Context.TearDown();
    }
}
```

**规则**：
- `Start` 中创建 Context + 注册系统 + Initialize
- `Update` 中 Execute + Cleanup
- `OnDestroy` 中 TearDown

---

## Step 4: 初始化中创建实体

在 `IInitializeSystem` 中创建游戏初始实体：

```csharp
public class GameInitSystem : IInitializeSystem
{
    private readonly Context m_Context;

    public GameInitSystem(Context context)
    {
        m_Context = context;
    }

    public void Initialize()
    {
        // 创建玩家实体
        var player = m_Context.CreateEntity();
        player.AddComponent(new PositionComponent { X = 0, Y = 0, Z = 0 });
        player.AddComponent(new VelocityComponent { X = 0, Y = 0, Z = 0 });
        player.AddComponent(new PlayerComponent());

        // 创建敌人实体
        for (int i = 0; i < 10; i++)
        {
            var enemy = m_Context.CreateEntity();
            enemy.AddComponent(new PositionComponent { X = Random.Range(-5f, 5f), Y = 0, Z = 0 });
            enemy.AddComponent(new VelocityComponent { X = Random.Range(-1f, 1f), Y = 0, Z = 0 });
            enemy.AddComponent(new EnemyComponent());
        }
    }
}
```

---

## Step 5: Group 查询

Group 自动维护匹配实体列表，组件增删时实时同步。

```csharp
// 获取 Group（重复获取会返回同一个实例）
var playerGroup = context.GetGroup(
    Matcher.AllOf(ComponentTypeIndex<PlayerComponent>.Index)
);

// 遍历 Group
foreach (var player in playerGroup.GetEntities())
{
    // ...
}

// 获取 Group 实体数量
int playerCount = playerGroup.Count;

// 监听 Group 变化（用 Collector）
var collector = playerGroup.CreateCollector(GroupEvent.Added);
foreach (var entity in collector.CollectedEntities)
{
    Debug.Log($"新玩家加入: {entity}");
}
collector.ClearCollectedEntities();
```

**Matcher 组合**：

```csharp
// 同时拥有 A 和 B
Matcher.AllOf(A, B)

// 拥有 A 或 B 至少一个
Matcher.AnyOf(A, B)

// 拥有 A 但不能有 B
Matcher.AllOf(A).NoneOf(B)

// 复杂组合
Matcher.AllOf(A, B).AnyOf(C, D).NoneOf(E)
```

---

## Step 6: 销毁实体

```csharp
// 方式1：直接销毁
entity.Destroy();

// 方式2：通过 Context 销毁
context.DestroyEntity(entity);

// 方式3：标记+延迟销毁（推荐）
entity.AddComponent(new DestroyComponent());  // 标记

// 在 DestroySystem 中统一处理
public class DestroySystem : ICleanupSystem
{
    private readonly Group m_DestroyGroup;

    public DestroySystem(Context context)
    {
        m_DestroyGroup = context.GetGroup(
            Matcher.AllOf(ComponentTypeIndex<DestroyComponent>.Index)
        );
    }

    public void Cleanup()
    {
        foreach (var entity in m_DestroyGroup.GetEntities())
        {
            context.DestroyEntity(entity);
        }
    }
}
```

**推荐方式3**：标记+延迟销毁避免遍历中修改集合的问题。

---

## Step 7: 实体组件操作

```csharp
// 添加组件（自动扩容）
entity.AddComponent(new HealthComponent { Value = 100 });

// 获取组件
var health = entity.GetComponent<HealthComponent>();

// 替换组件（触发 Replace 事件）
entity.ReplaceComponent(new HealthComponent { Value = 80 });

// 移除组件（触发 Removed 事件）
entity.RemoveComponent<HealthComponent>();

// 判断是否有组件
bool hasHealth = entity.HasComponent<HealthComponent>();

// 获取所有组件索引
int[] indices = entity.GetComponentIndices();
```

---

## 完整开发流程图

```
定义组件 → 写系统（初始化/执行/清理） → 创建Context → 注册系统
    ↑                                              ↓
    └──────── 调试/测试 ← 运行 ← Initialize/Execute/Cleanup
```

**每新增一个游戏功能**：
1. 需要新数据？→ **定义新组件**
2. 需要新逻辑？→ **写新系统**
3. 需要过滤实体？→ **用 Matcher 创建 Group**
4. 系统写好了？→ **在 GameController 中链式注册**
