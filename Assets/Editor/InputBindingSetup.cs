using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputBindingSetup
{
    [MenuItem("Tools/Add Right Shift Binding")]
    public static void AddRightShiftBinding()
    {
        string path = "Assets/AddressableResources/InputSystem_Actions.inputactions";
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        if (asset == null)
        {
            Debug.LogError("Failed to load InputActionAsset at " + path);
            return;
        }

        var sprintAction = asset.FindAction("Player/Sprint");
        if (sprintAction != null)
        {
            bool exists = false;
            foreach (var binding in sprintAction.bindings)
            {
                if (binding.path == "<Keyboard>/rightShift")
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                // AddBinding extension method is available in Unity.InputSystem
                sprintAction.AddBinding("<Keyboard>/rightShift", groups: "Keyboard&Mouse");
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log("Successfully added Right Shift binding to Player/Sprint.");
            }
            else
            {
                Debug.Log("Right Shift binding already exists for Player/Sprint.");
            }
        }
        else
        {
            Debug.LogError("Could not find action Player/Sprint.");
        }
    }
}
