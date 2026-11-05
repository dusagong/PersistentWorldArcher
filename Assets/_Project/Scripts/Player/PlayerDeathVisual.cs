using System.Collections.Generic;
using UnityEngine;

namespace PersistentWorld.Player
{
    // Presentation only: Health and ArcherController retain ownership of death and revival.
    [DisallowMultipleComponent]
    public sealed class PlayerDeathVisual : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private SpriteRenderer livingVisual;
        private bool livingVisualWasEnabled;
        private GameObject marker;
        private Transform ghost;
        private Vector3 ghostRestPosition;
        private float shownAt;

        public void Show(SpriteRenderer visual)
        {
            if (marker != null || visual == null) return;
            Sprite stoneSprite = LoadSprite("Characters/DeathGravestone");
            Sprite ghostSprite = LoadSprite("Characters/DeathGhost");
            if (stoneSprite == null || ghostSprite == null) return;

            livingVisual = visual;
            livingVisualWasEnabled = visual.enabled;
            Vector3 feet = new Vector3(visual.bounds.center.x, visual.bounds.min.y, transform.position.z);
            marker = new GameObject("Death marker");
            marker.transform.SetParent(transform, false);
            marker.transform.SetPositionAndRotation(feet, Quaternion.identity);
            CreatePart("Gravestone", stoneSprite, .5f, Vector3.zero, visual);
            ghostRestPosition = new Vector3(0, .59f, 0);
            ghost = CreatePart("Ghost", ghostSprite, .32f, ghostRestPosition, visual).transform;
            shownAt = Time.time;
            visual.enabled = false;
        }

        public void Hide()
        {
            if (marker == null) return;
            marker.SetActive(false);
            Destroy(marker);
            marker = null;
            ghost = null;
            if (livingVisual != null) livingVisual.enabled = livingVisualWasEnabled;
            livingVisual = null;
        }

        private void LateUpdate()
        {
            if (ghost == null) return;
            ghost.localPosition = ghostRestPosition + Vector3.up * (Mathf.Sin((Time.time - shownAt) * 2.5f) * .04f);
        }

        private GameObject CreatePart(string label, Sprite sprite, float height, Vector3 position, SpriteRenderer source)
        {
            GameObject part = new GameObject(label);
            part.transform.SetParent(marker.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder;
            return part;
        }

        // Calculate transparent padding once per shared sprite, never per frame or death.
        private static Sprite LoadSprite(string path)
        {
            if (Sprites.TryGetValue(path, out Sprite sprite) && sprite != null) return sprite;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null || !texture.isReadable) return null;
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a > 8)
                    {
                        minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                    }
            if (maxX < minX) return null;
            sprite = Sprite.Create(texture, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
            sprite.name = path.Substring(path.LastIndexOf('/') + 1);
            Sprites[path] = sprite;
            return sprite;
        }
    }
}
