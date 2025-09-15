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

        // 1) Iteratively collect all nodes in the tree (DFS), protect against cycles
        var stack = new Stack<GameObject>();
        var visited = new HashSet<Transform>();
        var order = new List<GameObject>(); // preliminary order

        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == null) continue;
            if (visited.Contains(node.transform)) continue;
            visited.Add(node.transform);
            order.Add(node);

            // push the children (in any order)
            for (int i = 0; i < node.transform.childCount; i++)
            {
                var ch = node.transform.GetChild(i);
                if (!visited.Contains(ch))
                    stack.Push(ch.gameObject);
            }
        }

        // 2) Process in reverse order — first the leaves, then up the tree
        for (int idx = order.Count - 1; idx >= 0; idx--)
        {
            var obj = order[idx];
            var t = obj.transform;
            if (t.childCount == 0) continue;

            // collect bounds for all renderers belonging to each direct child (including its subtree)
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

            if (!hasBounds) continue; // straight children have no geometry

            Vector3 center = bounds.center;

            // Undo entry (record the state before the change)
            // Register the parent and all direct children — this provides a convenient rollback
            var undoList = new List<Object> { obj.transform };
            for (int c = 0; c < t.childCount; c++)
                undoList.Add(t.GetChild(c));

            Undo.RecordObjects(undoList.ToArray(), "Center Pivot Iterative");

            // move the children so that nothing visually shifts, and move the node itself to the center
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

