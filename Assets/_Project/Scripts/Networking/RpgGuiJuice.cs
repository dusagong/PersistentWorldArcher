using System.Collections.Generic;
using PersistentWorld.UI;
using UnityEngine;

namespace PersistentWorld.Networking
{
    // OnGUI로 그리는 메뉴·로그인·파티 버튼의 손맛. GUI.Button / GUILayout.Button 자리에 그대로 바꿔 쓴다.
    // 마우스를 올리면 살짝 커지고, 누르면 눌렸다 튕기며 '또롱' 소리가 난다. 창이 열릴 때 쓰는 팝 효과도 여기 있다.
    internal static class RpgGuiJuice
    {
        private const float HoverScale = 1.05f;

        private sealed class State
        {
            public float Scale = 1f, PressedAt = -10f;
            public bool Hovered;
            public int SeenFrame;
        }

        private static readonly Dictionary<int, State> states = new Dictionary<int, State>();

        internal static bool Button(Rect rect, string text) => Button(rect, new GUIContent(text), GUI.skin.button);
        internal static bool Button(Rect rect, string text, GUIStyle style) => Button(rect, new GUIContent(text), style);

        internal static bool Button(Rect rect, GUIContent content, GUIStyle style)
        {
            Event e = Event.current;
            if (e.type == EventType.Layout) return GUI.Button(rect, content, style);
            State s = StateOf(rect, content);
            bool hover = GUI.enabled && rect.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                if (hover && !s.Hovered) UiSound.Hover();
                s.Hovered = hover;
                float target = hover ? HoverScale : 1f;
                s.Scale = Mathf.Lerp(s.Scale, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            }
            if (e.type == EventType.MouseDown && hover) s.PressedAt = Time.unscaledTime;

            float scale = s.Scale * Spring(Time.unscaledTime - s.PressedAt);
            Matrix4x4 previous = GUI.matrix;
            if (!Mathf.Approximately(scale, 1f)) GuiPivot.Scale(rect.center, scale);
            bool clicked = GUI.Button(rect, content, style);
            GUI.matrix = previous;
            if (clicked) UiSound.Click();
            return clicked;
        }

        internal static bool LayoutButton(string text, params GUILayoutOption[] options) => LayoutButton(text, GUI.skin.button, options);

        internal static bool LayoutButton(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            return Button(GUILayoutUtility.GetRect(content, style, options), content, style);
        }

        // 창이 열릴 때: 작게 시작해 살짝 넘쳤다가 제자리로. center를 기준으로 키운다.
        internal static Matrix4x4 Pop(Vector2 center, float age, float from = .9f)
        {
            float k = Mathf.LerpUnclamped(from, 1f, UiJuice.EaseOutBack(age / .26f));
            return Matrix4x4.TRS(center, Quaternion.identity, new Vector3(k, k, 1f)) * Matrix4x4.Translate(-center);
        }

        internal static float FadeIn(float age) => Mathf.Clamp01(age / .09f);

        // 누른 순간 0.92로 눌렸다가 튕기며 1로 돌아온다
        private static float Spring(float age)
        {
            if (age < 0f || age > .45f) return 1f;
            return 1f - .08f * Mathf.Exp(-age * 11f) * Mathf.Cos(age * 30f);
        }

        // 같은 자리·같은 글씨의 버튼은 프레임이 지나도 같은 상태를 쓴다
        private static State StateOf(Rect rect, GUIContent content)
        {
            int key = (Mathf.RoundToInt(rect.x) * 73856093) ^ (Mathf.RoundToInt(rect.y) * 19349663) ^ (Mathf.RoundToInt(rect.width) * 83492791) ^ (content.text ?? "").GetHashCode();
            if (!states.TryGetValue(key, out State s))
            {
                if (states.Count > 400) Prune();
                states[key] = s = new State();
            }
            s.SeenFrame = Time.frameCount;
            return s;
        }

        private static void Prune()
        {
            var stale = new List<int>();
            foreach (var pair in states) if (Time.frameCount - pair.Value.SeenFrame > 120) stale.Add(pair.Key);
            foreach (int key in stale) states.Remove(key);
        }
    }
}
