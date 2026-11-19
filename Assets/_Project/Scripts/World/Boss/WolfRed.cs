using UnityEngine;

namespace PersistentWorld.World
{
    // 늑대 2페이즈의 붉은 색 (0 = 초록 그대로, 1 = 완전히 빨강).
    // 그림은 WolfRedShift 셰이더 머티리얼(Resources/Boss/Wolf/WolfRedShift)이 전역 값 _WolfRed로 초록만 빨강으로 바꾸고,
    // 코드에서 정하는 단색(경고 장판·빛 번짐·화면 번쩍임 등)은 Tint로 같은 비율만큼 빨강 쪽으로 옮긴다.
    public static class WolfRed
    {
        private static readonly int Property = Shader.PropertyToID("_WolfRed");
        private static Material material;
        private static bool loaded;

        public static float Amount { get; private set; }

        public static void Set(float amount)
        {
            Amount = Mathf.Clamp01(amount);
            Shader.SetGlobalFloat(Property, Amount);
        }

        // 초록 그림이 빨강으로 바뀌도록 머티리얼을 입힌다 (머티리얼이 없으면 그대로 둔다)
        public static void Apply(SpriteRenderer renderer)
        {
            if (!loaded) { material = Resources.Load<Material>("Boss/Wolf/WolfRedShift"); loaded = true; }
            if (material != null && renderer != null) renderer.sharedMaterial = material;
        }

        // 연두·초록 계열 색인지 (빛나는 장식을 골라낼 때)
        public static bool IsGreen(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return h > .16f && h < .47f && s > .2f;
        }

        // 같은 밝기·채도의 빨강으로 (초록 계열이 아니면 그대로)
        public static Color Redden(Color c)
        {
            if (!IsGreen(c)) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            Color red = Color.HSVToRGB(0f, Mathf.Clamp01(s * 1.05f), v);
            red.a = c.a;
            return red;
        }

        // 초록 단색 → 빨강 단색 (알파는 초록 쪽 값을 따른다)
        public static Color Tint(Color green, Color red)
        {
            Color c = Color.Lerp(green, red, Amount);
            c.a = green.a;
            return c;
        }
    }
}
