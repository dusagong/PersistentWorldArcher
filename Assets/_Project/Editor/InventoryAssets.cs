using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.Editor
{
    public static class InventoryAssets
    {
        public static void Prepare()
        {
            const string path = "Assets/_Project/Resources/UI/BlacksmithPanel.asset";
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(path) != null) return;
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>("UI/BlacksmithTheme");
            if (theme == null) throw new InvalidOperationException("Missing blacksmith theme.");
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 720);
            settings.sortingOrder = 100;
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
        }
    }
}
