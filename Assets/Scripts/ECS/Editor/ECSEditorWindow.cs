using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EntitasLite.Editor
{
    /// <summary>
    /// ECS 调试窗口：运行时查看 Context、Entity、System 状态
    /// 打开方式：菜单栏 EntitasLite → ECS Debugger
    /// </summary>
    public class ECSEditorWindow : EditorWindow
    {
        private Vector2 m_MainScroll;
        private Vector2 m_EntityScroll;
        private Vector2 m_SystemScroll;

        private readonly Dictionary<Entity, bool> m_EntityFoldouts = new Dictionary<Entity, bool>();
        private Entity m_SelectedEntity;

        private static readonly Color s_HeaderColor = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color s_AlternateRowColor = new Color(0.25f, 0.25f, 0.25f);
        private static readonly Color s_SelectedColor = new Color(0.15f, 0.35f, 0.55f);

        [MenuItem("EntitasLite/ECS Debugger")]
        public static void OpenWindow()
        {
            var window = GetWindow<ECSEditorWindow>("ECS Debugger");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("ECS Debugger 仅在 Play 模式下可用", MessageType.Info);
                return;
            }

            m_MainScroll = EditorGUILayout.BeginScrollView(m_MainScroll);

            DrawToolbar();
            DrawContexts();
            DrawSystems();

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                Repaint();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear All Entities", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("确认", "销毁所有 Context 中的所有实体？", "确定", "取消"))
                {
                    foreach (var ctx in ContextRegistry.GetAllContexts())
                    {
                        ctx.DestroyAllEntities();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawContexts()
        {
            var contexts = ContextRegistry.GetAllContexts();
            if (contexts.Count == 0)
            {
                EditorGUILayout.HelpBox("没有注册的 Context", MessageType.Warning);
                return;
            }

            foreach (var context in contexts)
            {
                EditorGUILayout.Space(4);
                DrawContextHeader(context);
                DrawEntities(context);
            }
        }

        private void DrawContextHeader(Context context)
        {
            var bg = GUI.backgroundColor;
            GUI.backgroundColor = s_HeaderColor;

            EditorGUILayout.BeginVertical("box");
            GUI.backgroundColor = bg;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"📦 {context.Name}", EditorStyles.boldLabel, GUILayout.Width(200));
            EditorGUILayout.LabelField($"Entities: {context.Count}", GUILayout.Width(100));

            var systems = context.Systems;
            if (systems != null)
            {
                EditorGUILayout.LabelField($"Systems: {systems.GetSystems().Count}", GUILayout.Width(100));
                if (systems.ProfilingEnabled && systems.TotalExecuteTimeMs > 0)
                {
                    EditorGUILayout.LabelField($"Total: {systems.TotalExecuteTimeMs}ms", GUILayout.Width(100));
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntities(Context context)
        {
            var entities = context.GetEntities();
            if (entities.Length == 0)
            {
                EditorGUILayout.LabelField("  (无实体)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            m_EntityScroll = EditorGUILayout.BeginScrollView(m_EntityScroll, GUILayout.MaxHeight(300));

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || !entity.IsEnabled) continue;

                var rowBg = (i % 2 == 0) ? s_AlternateRowColor : Color.clear;
                if (entity == m_SelectedEntity)
                    rowBg = s_SelectedColor;

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = rowBg;

                EditorGUILayout.BeginHorizontal("box");
                GUI.backgroundColor = prevBg;

                if (!m_EntityFoldouts.ContainsKey(entity))
                    m_EntityFoldouts[entity] = false;

                m_EntityFoldouts[entity] = EditorGUILayout.Foldout(
                    m_EntityFoldouts[entity],
                    $"  Entity_{entity.CreationIndex}",
                    true);

                var indices = entity.GetComponentIndices();
                EditorGUILayout.LabelField($"Components: {indices.Length}", GUILayout.Width(100));

                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    m_SelectedEntity = entity;
                    Selection.activeObject = null;
                }

                if (GUILayout.Button("Destroy", GUILayout.Width(60)))
                {
                    context.DestroyEntity(entity);
                    m_EntityFoldouts.Remove(entity);
                    if (m_SelectedEntity == entity) m_SelectedEntity = null;
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (m_EntityFoldouts[entity])
                {
                    DrawEntityComponents(entity);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntityComponents(Entity entity)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;

            var components = entity.GetComponents();
            foreach (var component in components)
            {
                if (component == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• {component.GetType().Name}", EditorStyles.miniLabel);

                // 尝试显示组件的字段值（通过反射）
                DrawComponentFields(component);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        private void DrawComponentFields(IComponent component)
        {
            var type = component.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var field in fields)
            {
                var value = field.GetValue(component);
                var valueStr = value?.ToString() ?? "null";
                if (valueStr.Length > 40) valueStr = valueStr.Substring(0, 37) + "...";
                EditorGUILayout.LabelField($"  {field.Name} = {valueStr}", EditorStyles.miniLabel);
            }
        }

        private void DrawSystems()
        {
            var contexts = ContextRegistry.GetAllContexts();
            if (contexts.Count == 0) return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("⚙️ Systems Profile", EditorStyles.boldLabel);

            m_SystemScroll = EditorGUILayout.BeginScrollView(m_SystemScroll, GUILayout.MaxHeight(200));

            foreach (var context in contexts)
            {
                var systems = context.Systems;
                if (systems == null || !systems.ProfilingEnabled) continue;

                EditorGUILayout.LabelField($"Context: {context.Name}", EditorStyles.miniBoldLabel);

                var results = systems.ProfileResults;
                if (results.Count == 0)
                {
                    EditorGUILayout.LabelField("  暂无数据（需至少执行一次 Execute）", EditorStyles.miniLabel);
                    continue;
                }

                // 按耗时排序
                var sorted = results.OrderByDescending(r => r.ElapsedMilliseconds).ToList();
                long maxMs = sorted[0].ElapsedMilliseconds;
                if (maxMs == 0) maxMs = 1;

                foreach (var info in sorted)
                {
                    EditorGUILayout.BeginHorizontal();

                    float ratio = (float)info.ElapsedMilliseconds / maxMs;
                    var barRect = GUILayoutUtility.GetRect(100, 16, GUILayout.Width(100));
                    EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
                    var fillRect = new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height);
                    EditorGUI.DrawRect(fillRect, new Color(0.2f, 0.6f, 0.9f));

                    EditorGUILayout.LabelField($"{info.Name}", EditorStyles.miniLabel, GUILayout.Width(200));
                    EditorGUILayout.LabelField($"{info.ElapsedMilliseconds} ms", EditorStyles.miniLabel, GUILayout.Width(80));

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.LabelField($"Total Execute: {systems.TotalExecuteTimeMs} ms", EditorStyles.miniBoldLabel);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
