using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressablesPlayModeSetup
{
    [MenuItem("Tools/Set Addressables Play Mode (Fastest)")]
    public static void SetPlayMode()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        int index = settings.DataBuilders.FindIndex(b => b.GetType().Name == "BuildScriptFastMode");
        if (index >= 0)
        {
            settings.ActivePlayModeDataBuilderIndex = index;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("Addressables Play Mode set to Use Asset Database (fastest).");
        }
    }
}
