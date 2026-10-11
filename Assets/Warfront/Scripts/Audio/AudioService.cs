using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    public enum AudioCue
    {
        Click = 0,
        Confirm = 1,
        Back = 2,
        Deny = 3,
        Pickup = 4,
        UseItem = 5,
        WeaponSwap = 6,
        Dodge = 7,
        Jump = 8,
        Crouch = 9,
        Empty = 10,
        ObjectiveComplete = 11,
        LevelUp = 12,
        Extraction = 13,
        MissionSuccess = 14,
        MissionFail = 15,
        Hitmarker = 16,
        Alarm = 17
    }

    /// <summary>
    /// Audio service.
    ///
    /// Three channels (master/music/SFX), a small round-robin pool of positional sources for combat,
    /// looping ambient/music beds and alarm loops. All clips are generated procedurally at boot, so
    /// the game has original audio without shipping media files.
    /// </summary>
    public class AudioService
    {
        private const int SfxVoiceCount = 14;

        private readonly SettingsService _settings;
        private readonly GameObject _root;
        private readonly AudioSource _musicSource;
        private readonly AudioSource _ambientSource;
        private readonly AudioSource _alarmSource;
        private readonly AudioSource _uiSource;
        private readonly AudioSource[] _sfxVoices;
        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>(32);
        private readonly Dictionary<WeaponDef, AudioClip> _weaponClips = new Dictionary<WeaponDef, AudioClip>(16);

        private int _voiceIndex;
        private float _lastFootstepTime;
        private int _footstepSeed;
        private bool _musicIsMenu = true;
        private float _sfxVolume = 1f;
        private float _musicVolume = 0.6f;

        public AudioService(SettingsService settings, Transform parent)
        {
            _settings = settings;
            _root = new GameObject("WarfrontAudio");
            _root.transform.SetParent(parent, false);

            _musicSource = CreateSource("Music", true, 0.35f);
            _ambientSource = CreateSource("Ambient", true, 0.4f);
            _alarmSource = CreateSource("Alarm", true, 0.6f);
            _uiSource = CreateSource("Ui", false, 0.8f);

            _sfxVoices = new AudioSource[SfxVoiceCount];
            for (int i = 0; i < SfxVoiceCount; i++) _sfxVoices[i] = CreateSource("Sfx" + i, false, 1f);

            PrepareClips();
            ApplyVolumes();
        }

        private AudioSource CreateSource(string name, bool loop, float volume)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.dopplerLevel = 0f;
            return source;
        }

        private void PrepareClips()
        {
            Cache("ui_click", ProceduralAudio.SineBlip("ui_click", 880f, 0.06f, 0.25f, 26f));
            Cache("ui_confirm", ProceduralAudio.SineBlip("ui_confirm", 620f, 0.12f, 0.3f, 14f));
            Cache("ui_back", ProceduralAudio.SineBlip("ui_back", 380f, 0.1f, 0.26f, 18f));
            Cache("ui_deny", ProceduralAudio.SineBlip("ui_deny", 180f, 0.16f, 0.3f, 10f));
            Cache("ui_pickup", ProceduralAudio.SineBlip("ui_pickup", 1180f, 0.09f, 0.24f, 22f));
            Cache("ui_item", ProceduralAudio.TwoToneBeep("ui_item", 520f, 760f, 0.07f, 0.2f, 0.26f));
            Cache("ui_swap", ProceduralAudio.ReloadClick(4242, true));
            Cache("ui_dodge", ProceduralAudio.NoiseBurst("ui_dodge", 71, 0.22f, 12f, 0.5f, 0.4f, 0.4f));
            Cache("ui_jump", ProceduralAudio.NoiseBurst("ui_jump", 72, 0.14f, 18f, 0.6f, 0.2f, 0.3f));
            Cache("ui_crouch", ProceduralAudio.NoiseBurst("ui_crouch", 73, 0.1f, 26f, 0.7f, 0.15f, 0.25f));
            Cache("ui_empty", ProceduralAudio.ReloadClick(99, false));
            Cache("ui_objective", ProceduralAudio.TwoToneBeep("ui_objective", 740f, 1100f, 0.1f, 0.34f, 0.3f));
            Cache("ui_levelup", ProceduralAudio.TwoToneBeep("ui_levelup", 520f, 1040f, 0.14f, 0.6f, 0.32f));
            Cache("ui_hitmarker", ProceduralAudio.SineBlip("ui_hitmarker", 1400f, 0.05f, 0.2f, 30f));
            Cache("alarm", ProceduralAudio.TwoToneBeep("alarm", 560f, 860f, 0.28f, 1.6f, 0.34f));
            Cache("music_menu", ProceduralAudio.MusicLoop("music_menu", 8f, 0.5f));
            Cache("music_mission", ProceduralAudio.MusicLoop("music_mission", 8f, 0.62f));
            Cache("ambient_field", ProceduralAudio.AmbientLoop("ambient_field", 2024, 6f, 0.35f, 0.3f));
            Cache("stinger_success", ProceduralAudio.TwoToneBeep("stinger_success", 440f, 880f, 0.18f, 1.2f, 0.36f));
            Cache("stinger_fail", ProceduralAudio.NoiseBurst("stinger_fail", 5150, 1.2f, 3f, 0.65f, 0.5f, 0.5f));
        }

        private void Cache(string key, AudioClip clip)
        {
            if (clip == null || _clipCache.ContainsKey(key)) return;
            _clipCache[key] = clip;
        }

        private AudioClip Get(string key)
        {
            AudioClip clip;
            return _clipCache.TryGetValue(key, out clip) ? clip : null;
        }

        public void ApplyVolumes()
        {
            float master = _settings != null ? _settings.Data.MasterVolume : 1f;
            _sfxVolume = _settings != null ? _settings.Data.SfxVolume : 1f;
            _musicVolume = _settings != null ? _settings.Data.MusicVolume : 1f;

            AudioListener.volume = master;
            if (_musicSource != null) _musicSource.volume = 0.35f * _musicVolume;
            if (_ambientSource != null) _ambientSource.volume = 0.4f * _sfxVolume;
            if (_alarmSource != null) _alarmSource.volume = 0.55f * _sfxVolume;
            if (_uiSource != null) _uiSource.volume = 0.85f * _sfxVolume;
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                if (_sfxVoices[i] != null) _sfxVoices[i].volume = _sfxVolume;
            }
        }

        // ------------------------------------------------------------------ music

        public void SetMusicState(bool menu)
        {
            if (_musicIsMenu == menu && _musicSource != null && _musicSource.isPlaying) return;
            _musicIsMenu = menu;

            AudioClip clip = Get(menu ? "music_menu" : "music_mission");
            if (_musicSource == null || clip == null) return;
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;
            _musicSource.clip = clip;
            _musicSource.loop = true;
            _musicSource.Play();

            AudioClip ambient = Get("ambient_field");
            if (_ambientSource != null && ambient != null && !_ambientSource.isPlaying)
            {
                _ambientSource.clip = ambient;
                _ambientSource.loop = true;
                _ambientSource.Play();
            }
        }

        public void StopMissionBeds()
        {
            if (_alarmSource != null && _alarmSource.isPlaying) _alarmSource.Stop();
        }

        public void PlayMissionStinger(bool success, bool menu)
        {
            PlayUi(success ? AudioCue.MissionSuccess : AudioCue.MissionFail);
            if (!menu) SetMusicState(false);
        }

        // -------------------------------------------------------------------- sfx

        public void PlayUi(AudioCue cue)
        {
            string key;
            switch (cue)
            {
                case AudioCue.Confirm: key = "ui_confirm"; break;
                case AudioCue.Back: key = "ui_back"; break;
                case AudioCue.Deny: key = "ui_deny"; break;
                case AudioCue.Pickup: key = "ui_pickup"; break;
                case AudioCue.UseItem: key = "ui_item"; break;
                case AudioCue.WeaponSwap: key = "ui_swap"; break;
                case AudioCue.Dodge: key = "ui_dodge"; break;
                case AudioCue.Jump: key = "ui_jump"; break;
                case AudioCue.Crouch: key = "ui_crouch"; break;
                case AudioCue.Empty: key = "ui_empty"; break;
                case AudioCue.ObjectiveComplete: key = "ui_objective"; break;
                case AudioCue.LevelUp: key = "ui_levelup"; break;
                case AudioCue.Hitmarker: key = "ui_hitmarker"; break;
                case AudioCue.MissionSuccess: key = "stinger_success"; break;
                case AudioCue.MissionFail: key = "stinger_fail"; break;
                case AudioCue.Alarm: key = "alarm"; break;
                case AudioCue.Extraction: key = "alarm"; break;
                default: key = "ui_click"; break;
            }

            AudioClip clip = Get(key);
            if (clip == null || _uiSource == null) return;
            _uiSource.PlayOneShot(clip, 1f);
        }

        public void PlayWeaponShot(WeaponDef def, Vector3 position)
        {
            if (def == null) return;
            AudioClip clip;
            if (!_weaponClips.TryGetValue(def, out clip))
            {
                clip = ProceduralAudio.WeaponShot(def);
                _weaponClips[def] = clip;
            }
            PlaySpatial(clip, position, 1f, 90f);
        }

        public void PlayReload(WeaponDef def, Vector3 position)
        {
            int seed = def != null ? WfMath.Hash(def.Id) : 1;
            PlaySpatial(ProceduralAudio.ReloadClick(seed, true), position, 0.7f, 24f);
        }

        public void PlayEnemyShot(EnemyDef def, Vector3 position)
        {
            int seed = def != null ? WfMath.Hash(def.Id) + 31 : 31;
            AudioClip clip = ProceduralAudio.NoiseBurst("enemy_shot", seed, 0.2f, 15f, 0.35f, 0.5f, 0.7f);
            PlaySpatial(clip, position, 0.85f, 80f);
        }

        public void PlayImpact(Vector3 position, bool flesh)
        {
            PlaySpatial(ProceduralAudio.Impact(flesh), position, 0.8f, 45f);
        }

        public void PlayFootstep(float intensity, Vector3 position)
        {
            if (Time.time - _lastFootstepTime < 0.18f) return;
            _lastFootstepTime = Time.time;
            _footstepSeed++;
            PlaySpatial(ProceduralAudio.Footstep(_footstepSeed, intensity), position, 0.6f, 18f);
        }

        public void PlayEnemyAlert(Vector3 position, EnemyArchetype archetype)
        {
            PlaySpatial(ProceduralAudio.EnemyAlert(archetype), position, 0.7f, 42f);
        }

        public void PlayEnemyHit(Vector3 position, bool headshot)
        {
            PlaySpatial(ProceduralAudio.Impact(true), position, headshot ? 0.9f : 0.6f, 35f);
        }

        public void PlayEnemyDeath(Vector3 position, EnemyArchetype archetype)
        {
            PlaySpatial(ProceduralAudio.EnemyDeath(archetype), position, 0.8f, 45f);
        }

        public void PlayPlayerHit(float strength)
        {
            PlaySpatial(ProceduralAudio.Impact(true), Vector3.zero, WfMath.Lerp(0.4f, 0.9f, WfMath.Clamp01(strength)), 0f, true);
        }

        public void PlayPlayerDeath()
        {
            PlaySpatial(ProceduralAudio.EnemyDeath(EnemyArchetype.Heavy), Vector3.zero, 1f, 0f, true);
        }

        /// <summary>Extraction alarm loop: switched on while a hold is in progress.</summary>
        public void PlayExtractionAlarm(bool active)
        {
            if (_alarmSource == null) return;
            if (active)
            {
                if (_alarmSource.isPlaying) return;
                AudioClip clip = Get("alarm");
                if (clip == null) return;
                _alarmSource.clip = clip;
                _alarmSource.loop = true;
                _alarmSource.Play();
            }
            else if (_alarmSource.isPlaying)
            {
                _alarmSource.Stop();
            }
        }

        /// <summary>Round-robin positional playback - no allocations and a hard voice cap.</summary>
        private void PlaySpatial(AudioClip clip, Vector3 position, float volume, float maxDistance, bool force2D = false)
        {
            if (clip == null || _sfxVoices.Length == 0) return;

            AudioSource source = null;
            for (int i = 0; i < _sfxVoices.Length; i++)
            {
                int index = (_voiceIndex + i) % _sfxVoices.Length;
                if (_sfxVoices[index] != null && !_sfxVoices[index].isPlaying)
                {
                    source = _sfxVoices[index];
                    _voiceIndex = (index + 1) % _sfxVoices.Length;
                    break;
                }
            }

            if (source == null)
            {
                // All voices busy: drop quiet cues rather than stealing a loud one.
                if (volume < 0.7f) return;
                source = _sfxVoices[_voiceIndex];
                _voiceIndex = (_voiceIndex + 1) % _sfxVoices.Length;
                source.Stop();
            }

            source.transform.position = position;
            source.spatialBlend = force2D || maxDistance <= 0f ? 0f : 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = maxDistance > 0f ? maxDistance : 40f;
            source.volume = _sfxVolume * Mathf.Clamp01(volume);
            source.PlayOneShot(clip, 1f);
        }

        public int CachedClipCount { get { return _clipCache.Count + _weaponClips.Count; } }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            _clipCache.Clear();
            _weaponClips.Clear();
        }
    }
}
