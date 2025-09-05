using UnityEngine;
using UnityEditor;
using System.Text;

public class SceneHierarchyExporter : EditorWindow
{
    [MenuItem("Tools/Export Scene Hierarchy")]
    public static void ExportSceneHierarchy()
    {
        StringBuilder sb = new StringBuilder();
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            TraverseGameObject(root, sb, 0);
        }

        string output = sb.ToString();
        Debug.Log(output);
        // Optionally write to file:
        System.IO.File.WriteAllText("SceneHierarchy.txt", output);
    }

    private static void TraverseGameObject(GameObject obj, StringBuilder sb, int indent)
    {
        string indentStr = new string(' ', indent * 2);
        sb.AppendLine($"{indentStr}- {obj.name} [{obj.GetType().Name}]");

        Component[] components = obj.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp == null) continue;
            sb.AppendLine($"{indentStr}  • {comp.GetType().Name}");
        }

        foreach (Transform child in obj.transform)
        {
            TraverseGameObject(child.gameObject, sb, indent + 1);
        }
    }
}
