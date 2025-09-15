using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class CenterPivotTool : MonoBehaviour
{
    [MenuItem("Tools/Center Pivot Iterative")]
    static void CenterPivot()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("Select a root GameObject in the Hierarchy.");
            return;
        }

        GameObject root = Selection.activeGameObject;

        // 1) Итеративно собираем все узлы в дереве (DFS), защищаемся от циклов
        var stack = new Stack<GameObject>();
        var visited = new HashSet<Transform>();
        var order = new List<GameObject>(); // предварительный порядок

        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == null) continue;
            if (visited.Contains(node.transform)) continue;
            visited.Add(node.transform);
            order.Add(node);

            // пушим детей (любым порядком)
            for (int i = 0; i < node.transform.childCount; i++)
            {
                var ch = node.transform.GetChild(i);
                if (!visited.Contains(ch))
                    stack.Push(ch.gameObject);
            }
        }

        // 2) Обрабатываем в обратном порядке — сначала листья, затем вверх по дереву
        for (int idx = order.Count - 1; idx >= 0; idx--)
        {
            var obj = order[idx];
            var t = obj.transform;
            if (t.childCount == 0) continue;

            // собираем bounds по всем рендерерам, принадлежащим каждому прямому ребёнку (включая его поддерево)
            bool hasBounds = false;
            Bounds bounds = new Bounds();

            for (int c = 0; c < t.childCount; c++)
            {
                var child = t.GetChild(c);
                var renders = child.GetComponentsInChildren<Renderer>();
                foreach (var r in renders)
                {
                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds) continue; // у прямых детей нет геометрии

            Vector3 center = bounds.center;

            // Запись для Undo (фиксируем состояния перед изменением)
            // Регистрируем родителя и всех прямых детей — это даёт удобный откат
            var undoList = new List<Object> { obj.transform };
            for (int c = 0; c < t.childCount; c++)
                undoList.Add(t.GetChild(c));

            Undo.RecordObjects(undoList.ToArray(), "Center Pivot Iterative");

            // смещаем детей, чтобы визуально ничего не съехало, а сам узел перенести в center
            Vector3 delta = t.position - center;
            if (delta != Vector3.zero)
            {
                for (int c = 0; c < t.childCount; c++)
                {
                    var child = t.GetChild(c);
                    child.position += delta;
                }
                t.position = center;
            }
        }

        Debug.Log("Center Pivot Iterative: done.");
    }
}
