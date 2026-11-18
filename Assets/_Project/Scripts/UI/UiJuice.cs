using PersistentWorld.Networking;
using UnityEngine;
using UnityEngine.UIElements;

namespace PersistentWorld.UI
{
    // UI 손맛(주스): 버튼은 마우스를 올리면 살짝 커지고 누르면 눌렸다 튕긴다(Resources/UI/Juice.uss).
    // 창은 열릴 때 작게 시작해 살짝 넘쳤다가 제자리로 오며 나타난다. 각 동작에 짧은 효과음을 붙인다.
    public static class UiJuice
    {
        private static StyleSheet sheet;

        // 문서 루트에 한 번 붙이면 그 안의 모든 버튼에 적용된다
        public static void Attach(VisualElement root)
        {
            if (root == null) return;
            if (sheet == null) sheet = Resources.Load<StyleSheet>("UI/Juice");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (evt.target is Button button && button.enabledInHierarchy) UiSound.Hover();
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(evt =>
            {
                if (ButtonOf(evt.target as VisualElement) is Button button && button.enabledInHierarchy) UiSound.Click();
            }, TrickleDown.TrickleDown);
        }

        public static void PopIn(VisualElement panel)
        {
            if (panel == null) return;
            UiSound.Open();
            panel.style.scale = new Scale(new Vector3(.9f, .9f, 1f));
            panel.style.opacity = 0f;
            panel.experimental.animation.Start(0f, 1f, 260, (element, t) =>
            {
                float k = .9f + .1f * EaseOutBack(t);
                element.style.scale = new Scale(new Vector3(k, k, 1f));
                element.style.opacity = Mathf.Clamp01(t * 3f);
            }).OnCompleted(() =>
            {
                panel.style.scale = new Scale(Vector3.one);
                panel.style.opacity = 1f;
            });
        }

        // 목표를 살짝 넘었다가 돌아오는 곡선 (또롱)
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private static Button ButtonOf(VisualElement element)
        {
            for (VisualElement e = element; e != null; e = e.parent)
                if (e is Button button) return button;
            return null;
        }
    }

    // OnGUI에서 한 점을 중심으로 키우거나 돌린다. Unity 기본 함수(ScaleAroundPivot·RotateAroundPivot)는
    // 이미 걸려 있는 화면 배율(GUI.matrix)을 무시하고 중심을 잡아서, 배율이 1이 아닌 화면에서 위치가 어긋난다.
    // 기본 함수로 중심 변환만 만든 뒤 기존 배율 뒤에 곱해 이를 바로잡는다.
    public static class GuiPivot
    {
        public static void Scale(Vector2 pivot, float scale)
        {
            Matrix4x4 current = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
            GUI.matrix = current * GUI.matrix;
        }

        public static void Rotate(Vector2 pivot, float degrees)
        {
            Matrix4x4 current = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot(degrees, pivot);
            GUI.matrix = current * GUI.matrix;
        }
    }

    // UI 효과음. 효과음 음량 설정을 따른다. 서버(배치 모드)에서는 소리를 내지 않는다.
    public static class UiSound
    {
        private static AudioSource source;
        private static AudioClip hover, click, open, close;
        private static float lastHover;

        public static void Hover()
        {
            if (Time.unscaledTime - lastHover < .06f) return;
            lastHover = Time.unscaledTime;
            Play(ref hover, "UiHover", .45f);
        }

        public static void Click() => Play(ref click, "UiClick", .8f);
        public static void Open() => Play(ref open, "UiOpen", .7f);
        public static void Close() => Play(ref close, "UiClose", .6f);

        private static void Play(ref AudioClip clip, string name, float volume)
        {
            if (Application.isBatchMode) return;
            if (source == null)
            {
                var root = new GameObject("UI Sound");
                Object.DontDestroyOnLoad(root);
                source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            if (clip == null) clip = Resources.Load<AudioClip>("Audio/" + name);
            if (clip != null) source.PlayOneShot(clip, volume * GameAudio.EffectVolume);
        }
    }
}
