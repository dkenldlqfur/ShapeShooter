using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressablesSetup
{
    [MenuItem("Tools/Setup ShapeShooter Addressables")]
    public static void Setup()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
        }
            
        var group = settings.DefaultGroup;
        
        // Helper
        void AddAsset(string guid, string address)
        {
            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(address);
        }

        void AddLabel(string guid, string label)
        {
            var entry = settings.CreateOrMoveEntry(guid, group);
            settings.AddLabel(label);
            entry.SetLabel(label, true);
        }

        // 1. GameSettings
        var gsGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/GameData/GameSettings.asset");
        if (!string.IsNullOrEmpty(gsGuid)) AddAsset(gsGuid, "GameData/GameSettings");

        // 2. LevelData
        var levelGuids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/AddressableResources/LevelData" });
        foreach (var guid in levelGuids)
        {
            AddAsset(guid, AssetDatabase.GUIDToAssetPath(guid)); // Keep original address or base name, but actually wait: the plan says label="LevelData"
            AddLabel(guid, "LevelData");
        }

        // 3. Player
        var playerGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/Prefabs/Player.prefab");
        if (!string.IsNullOrEmpty(playerGuid)) AddAsset(playerGuid, "Prefabs/Player");

        // 4. Bullet
        var bulletGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/Prefabs/Bullet.prefab");
        if (!string.IsNullOrEmpty(bulletGuid)) AddAsset(bulletGuid, "Prefabs/Bullet");

        // 5. Particles
        var hitGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/Prefabs/Particles/HitParticle.prefab");
        if (!string.IsNullOrEmpty(hitGuid)) AddAsset(hitGuid, "Prefabs/Particles/HitParticle");

        var muzzleGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/Prefabs/Particles/MuzzleFlash.prefab");
        if (!string.IsNullOrEmpty(muzzleGuid)) AddAsset(muzzleGuid, "Prefabs/Particles/MuzzleFlash");

        // 6. InputSystem_Actions
        var inputGuid = AssetDatabase.AssetPathToGUID("Assets/AddressableResources/InputSystem_Actions.inputactions");
        if (!string.IsNullOrEmpty(inputGuid)) AddAsset(inputGuid, "InputSystem_Actions");

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log("Addressables setup completed.");
    }
}
