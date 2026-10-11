using System;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Typed event bus. Systems publish and subscribe instead of reaching for each other, which keeps
    /// the HUD, audio, mission runtime and tutorial decoupled without a scene-wide lookup anywhere.
    /// Handlers are cleared when a mission is torn down so stale listeners cannot leak across runs.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<MissionDef> MissionStarted;
        public static event Action<MissionSummary> MissionEnded;
        public static event Action<ObjectiveProgress> ObjectiveUpdated;
        public static event Action<ObjectiveProgress> ObjectiveCompleted;
        public static event Action<EnemyController, bool> EnemyKilled;
        public static event Action<ItemStack, int> LootCollected;
        public static event Action<string, int> InventoryChanged;
        public static event Action<DamageResult, HitZone> PlayerDamaged;
        public static event Action<float, float> PlayerHealthChanged;
        public static event Action<float, float> PlayerArmorChanged;
        public static event Action<WeaponModel, AmmoType> WeaponChanged;
        public static event Action WeaponFired;
        public static event Action WeaponReloadStarted;
        public static event Action WeaponReloadFinished;
        public static event Action<ExtractionStatus> ExtractionChanged;
        public static event Action<string> PlayerDied;
        public static event Action<MissionState> MissionStateChanged;
        public static event Action SettingsChangedEvent;
        public static event Action<string> Notice;
        public static event Action<Vector3> NoiseEmitted;
        /// <summary>Local player confirmed a hit (true when it killed the target).</summary>
        public static event Action<bool> HitConfirmed;
        public static event Action<string> EnemyAlerted;

        public static void RaiseMissionStarted(MissionDef def) { if (MissionStarted != null) MissionStarted(def); }
        public static void RaiseMissionEnded(MissionSummary summary) { if (MissionEnded != null) MissionEnded(summary); }
        public static void RaiseObjectiveUpdated(ObjectiveProgress progress) { if (ObjectiveUpdated != null) ObjectiveUpdated(progress); }
        public static void RaiseObjectiveCompleted(ObjectiveProgress progress) { if (ObjectiveCompleted != null) ObjectiveCompleted(progress); }
        public static void RaiseEnemyKilled(EnemyController enemy, bool headshot) { if (EnemyKilled != null) EnemyKilled(enemy, headshot); }
        public static void RaiseLootCollected(ItemStack stack, int value) { if (LootCollected != null) LootCollected(stack, value); }
        public static void RaiseInventoryChanged(string itemId, int total) { if (InventoryChanged != null) InventoryChanged(itemId, total); }
        public static void RaisePlayerDamaged(DamageResult result, HitZone zone) { if (PlayerDamaged != null) PlayerDamaged(result, zone); }
        public static void RaisePlayerHealthChanged(float health, float max) { if (PlayerHealthChanged != null) PlayerHealthChanged(health, max); }
        public static void RaisePlayerArmorChanged(float armor, float max) { if (PlayerArmorChanged != null) PlayerArmorChanged(armor, max); }
        public static void RaiseWeaponChanged(WeaponModel weapon, AmmoType ammoType) { if (WeaponChanged != null) WeaponChanged(weapon, ammoType); }
        public static void RaiseWeaponFired() { if (WeaponFired != null) WeaponFired(); }
        public static void RaiseWeaponReloadStarted() { if (WeaponReloadStarted != null) WeaponReloadStarted(); }
        public static void RaiseWeaponReloadFinished() { if (WeaponReloadFinished != null) WeaponReloadFinished(); }
        public static void RaiseExtractionChanged(ExtractionStatus status) { if (ExtractionChanged != null) ExtractionChanged(status); }
        public static void RaisePlayerDied(string cause) { if (PlayerDied != null) PlayerDied(cause); }
        public static void RaiseMissionStateChanged(MissionState state) { if (MissionStateChanged != null) MissionStateChanged(state); }
        public static void RaiseSettingsChanged() { if (SettingsChangedEvent != null) SettingsChangedEvent(); }
        public static void RaiseNotice(string message) { if (Notice != null) Notice(message); }
        public static void RaiseNoise(Vector3 position) { if (NoiseEmitted != null) NoiseEmitted(position); }
        public static void RaiseHitConfirmed(bool killed) { if (HitConfirmed != null) HitConfirmed(killed); }
        public static void RaiseEnemyAlerted(string callsign) { if (EnemyAlerted != null) EnemyAlerted(callsign); }

        /// <summary>Drops every subscription. Called between missions and on shutdown.</summary>
        public static void Clear()
        {
            MissionStarted = null;
            MissionEnded = null;
            ObjectiveUpdated = null;
            ObjectiveCompleted = null;
            EnemyKilled = null;
            LootCollected = null;
            InventoryChanged = null;
            PlayerDamaged = null;
            PlayerHealthChanged = null;
            PlayerArmorChanged = null;
            WeaponChanged = null;
            WeaponFired = null;
            WeaponReloadStarted = null;
            WeaponReloadFinished = null;
            ExtractionChanged = null;
            PlayerDied = null;
            MissionStateChanged = null;
            SettingsChangedEvent = null;
            Notice = null;
            NoiseEmitted = null;
            HitConfirmed = null;
            EnemyAlerted = null;
        }
    }
}
