using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Owns the save document and its file.
    ///
    /// Loads at boot through the recovery chain (primary -> backup -> fresh profile), autosaves on
    /// meaningful state changes and on every background/resume/quit event, and never lets a corrupt
    /// file crash the game or destroy progression.
    /// </summary>
    public class SaveService
    {
        private readonly SaveFileStore _store;
        private float _dirtyTimer;
        private bool _dirty;
        private float _lastSaveTime;

        public SaveService(string directory)
        {
            _store = new SaveFileStore(directory);
        }

        public SaveRoot Data { get; private set; }
        public SaveLoadResult LastLoad { get; private set; }
        public bool HasSaveFile { get { return _store.Exists; } }
        public int SaveCount { get; private set; }

        public void Load()
        {
            LastLoad = _store.Load(true);
            Data = LastLoad.Data ?? SaveRoot.CreateDefault();
            if (LastLoad.Warnings != null)
            {
                for (int i = 0; i < LastLoad.Warnings.Count; i++)
                {
                    Debug.LogWarning("[Warfront] save: " + LastLoad.Warnings[i]);
                }
            }
            if (!string.IsNullOrEmpty(LastLoad.Message)) Debug.Log("[Warfront] Save: " + LastLoad.Message);

            // A brand new profile needs to exist on disk immediately so a crash never loses the run.
            if (LastLoad.Status == SaveLoadStatus.CreatedDefault || LastLoad.Status == SaveLoadStatus.RecoveredFromCorruption)
            {
                SaveNow();
            }
        }

        public void MarkDirty()
        {
            _dirty = true;
            _dirtyTimer = 3f;
        }

        /// <summary>Called from the bootstrap update loop: coalesces autosaves.</summary>
        public void Tick(float deltaTime)
        {
            if (!_dirty) return;
            _dirtyTimer -= deltaTime;
            if (_dirtyTimer > 0f) return;
            SaveNow();
        }

        public bool SaveNow()
        {
            if (Data == null) Data = SaveRoot.CreateDefault();
            Data.SchemaVersion = SaveSchema.CurrentVersion;
            Data.TotalPlaySeconds = Data.Progression != null ? Data.Progression.TotalPlaySeconds : 0f;

            string json = SaveSerializer.Serialize(Data);
            if (string.IsNullOrEmpty(json)) return false;

            bool ok = _store.Write(json);
            if (ok)
            {
                SaveCount++;
                _lastSaveTime = Time.unscaledTime;
                _dirty = false;
                _dirtyTimer = 0f;
            }
            else
            {
                Debug.LogError("[Warfront] Save failed - keeping the previous file on disk.");
            }
            return ok;
        }

        public void ResetToDefault()
        {
            Data = SaveRoot.CreateDefault();
            _store.DeleteAll();
            SaveNow();
        }

        public float SecondsSinceSave { get { return Time.unscaledTime - _lastSaveTime; } }

        /// <summary>Flushes pending changes. Safe to call from pause/quit/background events.</summary>
        public void Flush()
        {
            SaveNow();
        }

        public string SaveDirectory { get { return _store.DirectoryPath; } }
        public string SavePath { get { return _store.PrimaryPath; } }
        public string BackupPath { get { return _store.BackupPath; } }

        /// <summary>Reads the save file size, used by the settings screen diagnostics.</summary>
        public long FileSizeBytes
        {
            get
            {
                try
                {
                    System.IO.FileInfo info = new System.IO.FileInfo(_store.PrimaryPath);
                    return info.Exists ? info.Length : 0L;
                }
                catch (System.Exception)
                {
                    return 0L;
                }
            }
        }
    }
}
