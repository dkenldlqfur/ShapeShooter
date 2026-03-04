using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressablesBuilder
{
    [MenuItem("Tools/Build Addressables")]
    public static void Build()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return;

        AddressableAssetSettings.BuildPlayerContent(out var result);
        Debug.Log("Addressables Build Complete. Error: " + string.IsNullOrEmpty(result.Error));
    }
}
