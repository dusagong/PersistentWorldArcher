using UnityEngine;

namespace PersistentWorld.World
{
    // 격자 스프라이트 시트(읽기 가능한 Texture2D)를 프레임 스프라이트로 자른다. 몬스터·보스 애니메이션이 함께 쓴다.
    public static class SpriteSheetSlicer
    {
        public static Texture2D Load(string resourcePath, int columns, int rows)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null || !texture.isReadable || texture.width % columns != 0 || texture.height % rows != 0) return null;
            return texture;
        }

        // 첫 프레임(기본 자세)의 알파 경계. 크기 기준(PPU)을 정할 때 쓴다.
        public static RectInt FirstFrameBounds(Texture2D texture, int columns, int rows) =>
            Bounds(texture.GetPixels32(), texture, columns, rows, 0, 1);

        // 모든 프레임을 같은 영역(전체 프레임 알파 경계의 합집합)으로 잘라 위치가 흔들리지 않게 한다.
        // 피벗은 첫 프레임의 가로 중심, 발에서 bodyHeight 절반 위로 맞춰 시트가 바뀌어도 기본 자세가 같은 자리에 온다.
        public static Sprite[] Slice(Texture2D texture, int columns, int rows, float ppu, int bodyHeight, string name)
        {
            if (texture == null) return null;
            int count = columns * rows;
            Color32[] pixels = texture.GetPixels32();
            RectInt union = Bounds(pixels, texture, columns, rows, 0, count);
            RectInt first = Bounds(pixels, texture, columns, rows, 0, 1);
            var pivot = new Vector2((first.center.x - union.x) / union.width, (first.y + bodyHeight * .5f - union.y) / union.height);
            int width = texture.width / columns, cellHeight = texture.height / rows;
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                int x = i % columns * width, y = (rows - 1 - i / columns) * cellHeight;
                sprites[i] = Sprite.Create(texture, new Rect(x + union.x, y + union.y, union.width, union.height), pivot, ppu, 0, SpriteMeshType.FullRect);
                sprites[i].name = name + "_" + i.ToString("00");
            }
            return sprites;
        }

        // 셀 안 좌표로 [from, to) 프레임의 알파 경계 합집합
        private static RectInt Bounds(Color32[] pixels, Texture2D texture, int columns, int rows, int from, int to)
        {
            int width = texture.width / columns, height = texture.height / rows;
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int i = from; i < to; i++)
            {
                int ox = i % columns * width, oy = (rows - 1 - i / columns) * height;
                for (int py = 0; py < height; py++)
                    for (int px = 0; px < width; px++)
                        if (pixels[(oy + py) * texture.width + ox + px].a > 8)
                        { minX = Mathf.Min(minX, px); minY = Mathf.Min(minY, py); maxX = Mathf.Max(maxX, px); maxY = Mathf.Max(maxY, py); }
            }
            return maxX < minX ? new RectInt(0, 0, width, height) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
