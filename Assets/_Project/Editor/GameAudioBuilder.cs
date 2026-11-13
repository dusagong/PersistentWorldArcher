using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PersistentWorld.Editor
{
    // Original, deterministic game audio. WAV assets are included in the client and need no external service.
    public static class GameAudioBuilder
    {
        private const int Rate = 22050;
        private const string Folder = "Assets/_Project/Resources/Audio";
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            Write("TownTheme", Theme(108, false), true);
            Write("FieldTheme", Theme(126, true), true);
            foreach (string name in new[] { "Bow", "Sword", "Magic", "Hit", "Hurt", "Skill", "Potion", "Death" }) Write(name, Effect(name), false);
            AssetDatabase.SaveAssets();
            Debug.Log("PWA_AUDIO_READY original_themes=2 effects=8");
        }
        private static float[] Theme(float bpm, bool combat)
        {
            float beat = 60 / bpm;
            float[] samples = new float[Mathf.RoundToInt(32 * beat * Rate)];
            int[] roots = { 45, 41, 48, 43, 45, 41, 43, 45 };
            int[] tune = { 76, 79, 81, 79, 77, 76, 72, 74, 76, 72, 71, 67, 69, 72, 74, 76 };
            for (int bar = 0; bar < 8; bar++)
            {
                int root = roots[bar];
                for (int step = 0; step < 8; step++)
                {
                    float start = (bar * 4 + step * .5f) * beat;
                    int third = bar == 0 || bar == 4 || bar == 7 ? 3 : 4;
                    int offset = new[] { 0, 7, 12, third + 12 }[step % 4];
                    Note(samples, root + 12 + offset, start, beat * .43f, combat ? .075f : .06f, 0);
                    if (step % 2 == 0) Note(samples, root, start, beat * .8f, .11f, 1);
                    if (combat) Noise(samples, start, .045f, step % 2 == 0 ? .055f : .025f, (uint)(bar * 8 + step + 1));
                }
                for (int step = 0; step < 4; step++) Note(samples, tune[(bar * 4 + step) % tune.Length] + (combat ? 0 : -12),
                    (bar * 4 + step) * beat, beat * .85f, combat ? .10f : .085f, 2);
            }
            return samples;
        }
        private static void Note(float[] target, int midi, float start, float duration, float volume, int voice)
        {
            float frequency = 440 * Mathf.Pow(2, (midi - 69) / 12f);
            int from = Mathf.RoundToInt(start * Rate), count = Mathf.RoundToInt(duration * Rate);
            for (int i = 0; i < count && from + i < target.Length; i++)
            {
                float t = (float)i / Rate, phase = 2 * Mathf.PI * frequency * t;
                float envelope = Mathf.Min(1, t / .012f) * Mathf.Pow(1 - (float)i / count, voice == 1 ? .8f : 1.6f);
                float tone = voice == 0 ? Mathf.Sin(phase) + .25f * Mathf.Sin(phase * 2) :
                    voice == 1 ? Mathf.Sin(phase) + .18f * Mathf.Sin(phase * 3) : Mathf.Sin(phase + .2f * Mathf.Sin(phase * 2));
                target[from + i] += tone * volume * envelope;
            }
        }
        private static void Noise(float[] target, float start, float duration, float volume, uint seed)
        {
            int from = Mathf.RoundToInt(start * Rate), count = Mathf.RoundToInt(duration * Rate); float previous = 0;
            for (int i = 0; i < count && from + i < target.Length; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                float random = (seed & 65535) / 32767.5f - 1;
                previous = previous * .7f + random * .3f;
                target[from + i] += previous * Mathf.Pow(1 - (float)i / count, 2) * volume;
            }
        }
        private static float[] Effect(string name)
        {
            float seconds = name == "Death" ? .7f : name == "Potion" ? .45f : .22f;
            float[] samples = new float[Mathf.RoundToInt(seconds * Rate)];
            if (name == "Sword" || name == "Hit" || name == "Hurt") Noise(samples, 0, seconds, name == "Sword" ? .8f : .6f, 9427);
            if (name == "Potion") { Note(samples, 81, 0, .25f, .35f, 0); Note(samples, 88, .10f, .3f, .3f, 0); }
            else if (name == "Skill" || name == "Magic") { Note(samples, 76, 0, .22f, .3f, 2); Note(samples, 83, .04f, .17f, .2f, 2); }
            else
            {
                float baseHz = name == "Bow" ? 650 : name == "Death" ? 240 : name == "Hurt" ? 110 : name == "Hit" ? 180 : 350;
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / Rate, phase = 2 * Mathf.PI * baseHz * t * (1 - .6f * t / seconds);
                    samples[i] += Mathf.Sin(phase) * Mathf.Min(1, t / .004f) * Mathf.Pow(1 - t / seconds, 2) * .4f;
                }
            }
            return samples;
        }
        private static void Write(string name, float[] samples, bool music)
        {
            string path = Folder + "/" + name + ".wav";
            using (var stream = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + samples.Length * 2);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16); stream.Write((short)1); stream.Write((short)1);
                stream.Write(Rate); stream.Write(Rate * 2); stream.Write((short)2); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(samples.Length * 2);
                foreach (float sample in samples) stream.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -.95f, .95f) * 32767));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path); var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .8f;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
        }
    }
}
