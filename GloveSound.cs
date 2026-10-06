using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace QuickGloves
{
    // sounds live in UserData\QuickGloves\Sounds.
    // target, lock, pull, catch, cancel, beam (loops).
    // want variety? add pull_1.wav, pull_2.wav ...
    public static class GloveSound
    {
        public const string Target = "target", Lock = "lock", Pull = "pull", Catch = "catch", Cancel = "cancel", Beam = "beam";
        private static readonly string[] Names = { Target, Lock, Pull, Catch, Cancel, Beam };
        private static readonly string[] Exts  = { ".wav", ".ogg", ".mp3" };

        private static string _dir = "";
        private static readonly Dictionary<string, List<AudioClip>> _clips = new Dictionary<string, List<AudioClip>>();
        private static bool _loaded;

        private static AudioSource? _shotL, _shotR, _loopL, _loopR;

        public static void Initialize()
        {
            try
            {
                _dir = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "QuickGloves", "Sounds");
                Directory.CreateDirectory(_dir);
            }
            catch (Exception e) { MelonLogger.Warning("[QuickGloves] sound init: " + e.Message); }
        }

        public static void Reload() { _clips.Clear(); _loaded = false; EnsureLoaded(); }

        public static void Play(string name, bool isLeft, Vector3 pos, float volume = 1f)
        {
            if (!GloveSettings.Sounds) return;
            try
            {
                EnsureBuilt(); EnsureLoaded();
                AudioSource? src = isLeft ? _shotL : _shotR;
                if (src == null || !_clips.TryGetValue(name, out var list) || list.Count == 0) return;
                AudioClip clip = list[UnityEngine.Random.Range(0, list.Count)];
                if (clip == null) return;
                src.transform.position = pos;
                src.PlayOneShot(clip, Mathf.Clamp01(GloveSettings.SoundVolume * volume));
            }
            catch { }
        }

        // leash hum, whinier when it's stuck
        public static void SetBeam(bool isLeft, bool active, Vector3 pos, bool strained = false)
        {
            try
            {
                if (!GloveSettings.Sounds) active = false;
                if (!active)
                {
                    AudioSource? cur = isLeft ? _loopL : _loopR;
                    if (cur != null && cur.isPlaying) cur.Stop();
                    return;
                }

                EnsureBuilt(); EnsureLoaded();
                AudioSource? src = isLeft ? _loopL : _loopR;
                if (src == null || !_clips.TryGetValue(Beam, out var list) || list.Count == 0) return;
                src.transform.position = pos;
                src.volume = Mathf.Clamp01(GloveSettings.SoundVolume * (strained ? 0.5f : 0.3f));
                src.pitch  = strained ? 1.3f : 1f;
                if (!src.isPlaying) { src.clip = list[0]; src.Play(); }
            }
            catch { }
        }

        // rebuild em if a level ate them
        private static void EnsureBuilt()
        {
            if (_shotL == null) _shotL = MakeSource("[QuickGloves] SFX L", false);
            if (_shotR == null) _shotR = MakeSource("[QuickGloves] SFX R", false);
            if (_loopL == null) _loopL = MakeSource("[QuickGloves] Beam L", true);
            if (_loopR == null) _loopR = MakeSource("[QuickGloves] Beam R", true);
        }

        private static AudioSource MakeSource(string name, bool loop)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.loop         = loop;
            src.spatialBlend = 1f;
            src.minDistance  = 1f;
            src.maxDistance  = 25f;
            return src;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            int total = 0;
            foreach (string name in Names)
            {
                var list = new List<AudioClip>();
                try
                {
                    foreach (string path in Directory.GetFiles(_dir).OrderBy(p => p))
                    {
                        string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                        string ext  = Path.GetExtension(path).ToLowerInvariant();
                        if (!Exts.Contains(ext)) continue;
                        if (file != name && !file.StartsWith(name + "_")) continue;
                        AudioClip? clip = Load(path);
                        if (clip == null) continue;
                        clip.hideFlags = HideFlags.DontUnloadUnusedAsset;   // don't let level loads eat it
                        list.Add(clip);
                    }
                }
                catch { }
                _clips[name] = list;
                total += list.Count;
            }
            MelonLogger.Msg($"[QuickGloves] {total} sounds loaded");
        }

        private static AudioClip? Load(string path)
        {
            try
            {
                AudioClip? viaLib = TryAudioImportLib(path);
                if (viaLib != null) return viaLib;
                if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    return ParseWav(File.ReadAllBytes(path), Path.GetFileNameWithoutExtension(path));
            }
            catch (Exception e) { MelonLogger.Warning("[QuickGloves] sound load: " + e.Message); }
            return null;
        }

        // bonus: ogg and mp3 if you have the lib
        private static bool _apiChecked;
        private static MethodInfo? _loadMethod;

        private static AudioClip? TryAudioImportLib(string path)
        {
            try
            {
                if (!_apiChecked)
                {
                    _apiChecked = true;
                    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "AudioImportLib");
                    _loadMethod = asm?.GetType("AudioImportLib.API")?.GetMethod("LoadAudioClip", BindingFlags.Public | BindingFlags.Static);
                }
                if (_loadMethod == null) return null;
                return _loadMethod.Invoke(null, new object[] { path, true }) as AudioClip;
            }
            catch { return null; }
        }

        // plain old wav reader, just in case
        private static AudioClip? ParseWav(byte[] data, string clipName)
        {
            if (data.Length < 12 || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F') return null;

            int channels = 1, sampleRate = 44100, bits = 16;
            byte[]? pcm = null;
            int pos = 12;
            while (pos + 8 <= data.Length)
            {
                char c0 = (char)data[pos], c1 = (char)data[pos + 1], c2 = (char)data[pos + 2], c3 = (char)data[pos + 3];
                int size = BitConverter.ToInt32(data, pos + 4);
                int body = pos + 8;
                if (body + size > data.Length) size = data.Length - body;
                if (c0 == 'f' && c1 == 'm' && c2 == 't' && c3 == ' ')
                {
                    channels   = BitConverter.ToInt16(data, body + 2);
                    sampleRate = BitConverter.ToInt32(data, body + 4);
                    bits       = BitConverter.ToInt16(data, body + 14);
                }
                else if (c0 == 'd' && c1 == 'a' && c2 == 't' && c3 == 'a')
                {
                    pcm = new byte[size];
                    Array.Copy(data, body, pcm, 0, size);
                }
                pos = body + size + (size % 2);
            }
            if (pcm == null || pcm.Length == 0) return null;

            int bytesPer = Mathf.Max(1, bits / 8);
            int total = pcm.Length / bytesPer;
            var samples = new float[total];
            switch (bits)
            {
                case 8:  for (int i = 0; i < total; i++) samples[i] = (pcm[i] - 128) / 128f; break;
                case 16: for (int i = 0; i < total; i++) samples[i] = BitConverter.ToInt16(pcm, i * 2) / 32768f; break;
                case 32: for (int i = 0; i < total; i++) samples[i] = BitConverter.ToInt32(pcm, i * 4) / 2147483648f; break;
                default: return null;
            }
            var clip = AudioClip.Create(clipName, total / Mathf.Max(1, channels), channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}

// imagine reading comments
