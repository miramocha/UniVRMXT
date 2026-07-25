using UniVRMXT.MaterialsOverride;
using UnityEditor;
using UnityEngine;

namespace UniVRMXT.Editor.MaterialsOverride
{
    /// <summary>
    /// Menu entry for dumping materials-override apply state without hunting the component.
    /// </summary>
    public static class VrmxtMaterialsOverrideDebugMenu
    {
        [MenuItem("GameObject/VRMXT/Dump Materials Override Debug", false, 49)]
        private static void DumpFromSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogWarning("VRMXT materials debug: select a GameObject first.");
                return;
            }

            var store = go.GetComponentInChildren<VrmxtMaterialsOverrideInstance>(true);
            if (store == null)
            {
                Debug.LogWarning(
                    "VRMXT materials debug: no VrmxtMaterialsOverrideInstance under '"
                        + go.name
                        + "'."
                );
                return;
            }

            VrmxtMaterialsOverrideDebug.Dump(store);
        }

        [MenuItem("GameObject/VRMXT/Dump Materials Override Debug", true)]
        private static bool DumpFromSelectionValidate()
        {
            return Selection.activeGameObject != null;
        }
    }
}
