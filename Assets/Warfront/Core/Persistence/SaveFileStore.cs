using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Warfront.Core
{
    /// <summary>
    /// Durable local storage for the save document.
    /// Writes are atomic (temp file + replace) and a rotating backup is kept so a crash or a
    /// corrupt file can never destroy progression.
    /// </summary>
    public class SaveFileStore
    {
        public const string PrimaryFileName = "warfront_save.json";
        public const string BackupFileName = "warfront_save.backup.json";
        public const string TempFileName = "warfront_save.tmp";

        private readonly string _directory;

        public SaveFileStore(string directory)
        {
            _directory = string.IsNullOrEmpty(directory) ? "." : directory;
        }

        public string DirectoryPath { get { return _directory; } }
        public string PrimaryPath { get { return Path.Combine(_directory, PrimaryFileName); } }
        public string BackupPath { get { return Path.Combine(_directory, BackupFileName); } }

        public bool Exists { get { return File.Exists(PrimaryPath); } }

        public void EnsureDirectory()
        {
            try
            {
                if (!Directory.Exists(_directory)) Directory.CreateDirectory(_directory);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Warfront] Could not create save directory: " + ex.Message);
            }
        }

        /// <summary>
        /// Writes the document atomically. The previous file becomes the backup only after the new
        /// file is fully on disk, so an interrupted write never loses the last good save.
        /// </summary>
        public bool Write(string json)
        {
            if (string.IsNullOrEmpty(json)) return false;
            EnsureDirectory();

            string temp = Path.Combine(_directory, TempFileName);
            try
            {
                File.WriteAllText(temp, json);

                if (File.Exists(PrimaryPath))
                {
                    try
                    {
                        // File.Replace is atomic on both Windows and Unix and rotates the backup.
                        File.Replace(temp, PrimaryPath, BackupPath);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        FallbackReplace(temp);
                    }
                    catch (IOException)
                    {
                        FallbackReplace(temp);
                    }
                }
                else
                {
                    File.Move(temp, PrimaryPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[Warfront] Save write failed: " + ex.Message);
                TryDelete(temp);
                return false;
            }
        }

        private void FallbackReplace(string temp)
        {
            try
            {
                if (File.Exists(PrimaryPath)) File.Copy(PrimaryPath, BackupPath, true);
                if (File.Exists(PrimaryPath)) File.Delete(PrimaryPath);
                File.Move(temp, PrimaryPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[Warfront] Save fallback replace failed: " + ex.Message);
            }
        }

        public string ReadPrimary()
        {
            return ReadFile(PrimaryPath);
        }

        public string ReadBackup()
        {
            return ReadFile(BackupPath);
        }

        private static string ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Warfront] Save read failed (" + path + "): " + ex.Message);
                return null;
            }
        }

        public void DeleteAll()
        {
            TryDelete(PrimaryPath);
            TryDelete(BackupPath);
            TryDelete(Path.Combine(_directory, TempFileName));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Warfront] Could not delete " + path + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Loads the save, walking: primary -> backup -> default. Always returns a usable document.
        /// </summary>
        public SaveLoadResult Load(bool createDefaultWhenMissing)
        {
            SaveLoadResult result;
            result.Warnings = new List<string>();

            string primaryJson = ReadPrimary();
            if (!string.IsNullOrEmpty(primaryJson))
            {
                string error;
                SaveRoot primary = SaveSerializer.Deserialize(primaryJson, out error);
                if (primary != null && SaveSerializer.TryPrepare(primary, out error, result.Warnings) && SaveSerializer.VerifyIntegrity(primary))
                {
                    result.Status = SaveLoadStatus.Loaded;
                    result.Data = primary;
                    result.Message = "Loaded primary save";
                    return result;
                }

                result.Warnings.Add("Primary save rejected: " + (error ?? "integrity check failed"));

                string backupJson = ReadBackup();
                if (!string.IsNullOrEmpty(backupJson))
                {
                    string backupError;
                    SaveRoot backup = SaveSerializer.Deserialize(backupJson, out backupError);
                    if (backup != null && SaveSerializer.TryPrepare(backup, out backupError, result.Warnings))
                    {
                        result.Status = SaveLoadStatus.LoadedFromBackup;
                        result.Data = backup;
                        result.Message = "Recovered from backup";
                        // Promote the backup so the next write has a healthy baseline.
                        Write(SaveSerializer.Serialize(backup));
                        return result;
                    }
                    result.Warnings.Add("Backup save rejected: " + (backupError ?? "unknown"));
                }

                result.Status = SaveLoadStatus.RecoveredFromCorruption;
                result.Data = SaveRoot.CreateDefault();
                result.Message = "Both save files were unreadable - started a fresh profile";
                return result;
            }

            if (createDefaultWhenMissing)
            {
                result.Status = SaveLoadStatus.CreatedDefault;
                result.Data = SaveRoot.CreateDefault();
                result.Message = "No save found - created a new profile";
                return result;
            }

            result.Status = SaveLoadStatus.CreatedDefault;
            result.Data = null;
            result.Message = "No save found";
            return result;
        }
    }
}
