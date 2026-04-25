using UnityEngine;

namespace EntitasLite.Example
{
    /// <summary>
    /// ECS框架示例控制器，挂载到场景中的GameObject上
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private Context m_Context;

        void Start()
        {
            // 创建上下文（无需预先指定组件总数，自动扩容）
            m_Context = new Context("Game");

            // 链式添加系统
            m_Context
                .AddSystem(new GameInitSystem(m_Context))
                .AddSystem(new MoveSystem(m_Context))
                .AddSystem(new BoundarySystem(m_Context))
                .AddSystem(new LogSystem(m_Context))
                .AddSystem(new DestroySystem(m_Context))
                .AddSystem(new HealthAddedSystem(m_Context));

            // 初始化
            m_Context.Initialize();

            Debug.Log("[GameController] ECS框架已启动");
        }

        void Update()
        {
            // 每帧执行系统
            m_Context?.Execute();
        }

        void LateUpdate()
        {
            // 清理系统（如销毁标记为Destroy的实体）
            m_Context?.Cleanup();
        }

        void OnDestroy()
        {
            // 销毁所有系统并重置上下文
            m_Context?.TearDown();
            m_Context?.Reset();
            Debug.Log("[GameController] ECS框架已销毁");
        }

    }
}
