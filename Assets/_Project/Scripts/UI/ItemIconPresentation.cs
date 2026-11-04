using System;
using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.UI
{
    internal static class ItemIconPresentation
    {
        [Serializable] private sealed class BoundsData { public Entry[] items; }
        [Serializable] private sealed class Entry
        {
            public string path;
            public float x, y, width, height;
        }
        private static readonly Dictionary<Sprite, Sprite> views = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Sprite, Rect> bounds = new Dictionary<Sprite, Rect>();
        private static bool loaded;

        public static Sprite GetDisplaySprite(Sprite source)
        {
            if (source == null || source.packed) return source;
            if (views.TryGetValue(source, out Sprite view)) return view;
            if (!loaded)
            {
                loaded = true;
                var data = Resources.Load<TextAsset>("UI/ItemIconBounds");
                if (data != null)
                    foreach (Entry entry in JsonUtility.FromJson<BoundsData>(data.text).items)
                    {
                        Sprite sprite = Resources.Load<Sprite>(entry.path);
                        if (sprite != null) bounds[sprite] = new Rect(entry.x, entry.y, entry.width, entry.height);
                    }
            }
            if (!bounds.TryGetValue(source, out Rect crop)) return source;
            Rect rect = source.rect;
            view = Sprite.Create(source.texture,
                new Rect(rect.x + crop.x * rect.width, rect.y + crop.y * rect.height,
                    crop.width * rect.width, crop.height * rect.height),
                new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            view.name = source.name + " (UI)";
            views[source] = view;
            return view;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (Sprite sprite in views.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            views.Clear(); bounds.Clear(); loaded = false;
        }
    }
}
