using PersistentWorld.Player;
using UnityEngine;

namespace PersistentWorld.Networking
{
    public static class GameAudio
    {
        private static AudioSource music, effects;
        private static AudioClip town, field, bow, sword, magic, hit, hurt, skill, potion, death;
        private static float lastHit;
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("pwa-music-volume", .3f);
            set { PlayerPrefs.SetFloat("pwa-music-volume", Mathf.Clamp01(value)); if (music != null) music.volume = Mathf.Clamp01(value); }
        }
        public static float EffectVolume
        {
            get => PlayerPrefs.GetFloat("pwa-effect-volume", .65f);
            set { PlayerPrefs.SetFloat("pwa-effect-volume", Mathf.Clamp01(value)); if (effects != null) effects.volume = Mathf.Clamp01(value); }
        }
        private static AudioClip Clip(string name) => Resources.Load<AudioClip>("Audio/" + name);
        private static void Ensure()
        {
            if (effects != null || Application.isBatchMode) return;
            var root = new GameObject("Game Audio"); Object.DontDestroyOnLoad(root);
            effects = root.AddComponent<AudioSource>(); effects.spatialBlend = 0; effects.volume = EffectVolume;
            if (Object.FindFirstObjectByType<AudioListener>() == null) (Camera.main != null ? Camera.main.gameObject : root).AddComponent<AudioListener>();
            music = root.AddComponent<AudioSource>(); music.loop = true; music.volume = MusicVolume;
            town = Clip("TownTheme"); field = Clip("FieldTheme"); bow = Clip("Bow"); sword = Clip("Sword"); magic = Clip("Magic");
            hit = Clip("Hit"); hurt = Clip("Hurt"); skill = Clip("Skill"); potion = Clip("Potion"); death = Clip("Death");
            Debug.Log("PWA_AUDIO_LOADED themes=" + (town != null && field != null) + " effects=" + (bow != null && sword != null && hit != null && hurt != null));
        }
        public static void StartMusic() => SetArea(GameArea.Town);
        public static void SetArea(GameArea area)
        {
            Ensure(); if (music == null) return;
            AudioClip clip = area == GameArea.Town || area == GameArea.Tutorial ? town : field;
            if (clip == music.clip && music.isPlaying) return;
            music.clip = clip; if (clip != null) music.Play();
        }
        private static void Play(AudioClip clip) { if (effects != null && clip != null) effects.PlayOneShot(clip); }
        public static void Hit(bool player)
        {
            Ensure(); if (Time.unscaledTime - lastHit < .06f) return;
            lastHit = Time.unscaledTime; Play(player ? hurt : hit);
        }
        public static void Attack(CharacterClass type) { Ensure(); Play(type == CharacterClass.Warrior ? sword : type == CharacterClass.Healer ? magic : bow); }
        public static void Action(bool isSkill = false, bool isPotion = false) { Ensure(); Play(isPotion ? potion : isSkill ? skill : bow); }
        public static void Death() { Ensure(); Play(death); }
        public static void DrawSettings()
        {
            GUILayout.Label($"BGM 음량 {MusicVolume * 100:0}%");
            float next = GUILayout.HorizontalSlider(MusicVolume, 0, 1); if (!Mathf.Approximately(next, MusicVolume)) MusicVolume = next;
            GUILayout.Label($"효과음 음량 {EffectVolume * 100:0}%");
            next = GUILayout.HorizontalSlider(EffectVolume, 0, 1); if (!Mathf.Approximately(next, EffectVolume)) EffectVolume = next;
        }
    }
}