using UnityEngine;

namespace PersistentWorld.Core
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlaceholderVisual : MonoBehaviour
    {
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Vector2 size = Vector2.one;

        private static Sprite squareSprite;

        private void Awake()
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = GetSquareSprite();
            renderer.color = color;
            transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        public void Configure(Color newColor, Vector2 newSize)
        {
            color = newColor;
            size = newSize;

            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = GetSquareSprite();
                renderer.color = color;
                transform.localScale = new Vector3(size.x, size.y, 1f);
            }
        }

        private static Sprite GetSquareSprite()
        {
            if (squareSprite != null)
            {
                return squareSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Runtime Placeholder Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            squareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            squareSprite.name = "Runtime Placeholder Sprite";
            return squareSprite;
        }
    }
}

