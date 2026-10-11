using UnityEngine;

namespace Warfront.Core
{
    public enum WeaponState
    {
        Ready = 0,
        Firing = 1,
        Reloading = 2,
        Equipping = 3,
        Empty = 4,
        Disabled = 5
    }

    public struct FireAttempt
    {
        public bool Fired;
        public int Shots;
        public float SpreadDegrees;
        public float RecoilVertical;
        public float RecoilHorizontal;
        public int AmmoInMagazine;
        public bool TriggeredReloadHint;
    }

    /// <summary>
    /// Deterministic weapon runtime: ammo, cadence, reload, spread and recoil accumulation.
    /// Time is injected (seconds) so the whole model is unit testable and network ready later.
    /// </summary>
    public class WeaponModel
    {
        private readonly WeaponDef _def;
        private WeaponStats _stats;
        private Rarity _rarity;
        private int _upgradeLevel;
        private WfRandom _recoilRandom;

        private float _nextShotTime;
        private float _stateEndTime;
        private float _stateStartTime;
        private float _spread;
        private int _burstRemaining;
        private int _recoilShotIndex;

        public WeaponModel(WeaponDef def, Rarity rarity, int upgradeLevel, int recoilSeed)
        {
            _def = def;
            _rarity = rarity;
            _upgradeLevel = WfMath.ClampInt(upgradeLevel, 1, Mathf.Max(1, def.MaxUpgradeLevel));
            _stats = WeaponStatRules.Build(def, _rarity, _upgradeLevel);
            _ammoInMagazine = _stats.MagazineSize;
            _recoilRandom = new WfRandom(recoilSeed == 0 ? WfMath.Hash(def.Id) : recoilSeed);
            State = WeaponState.Ready;
        }

        public WeaponDef Def { get { return _def; } }
        public WeaponStats Stats { get { return _stats; } }
        public Rarity Rarity { get { return _rarity; } }
        public int UpgradeLevel { get { return _upgradeLevel; } }
        public WeaponState State { get; private set; }

        private int _ammoInMagazine;
        public int AmmoInMagazine { get { return _ammoInMagazine; } }
        public int MagazineSize { get { return _stats.MagazineSize; } }
        public bool MagazineEmpty { get { return _ammoInMagazine <= 0; } }
        public bool MagazineFull { get { return _ammoInMagazine >= _stats.MagazineSize; } }
        public float CurrentSpread { get { return _spread; } }
        public float ReloadProgress01 { get; private set; }
        public float EquipProgress01 { get; private set; }

        public void SetUpgradeLevel(int level)
        {
            int clamped = WfMath.ClampInt(level, 1, Mathf.Max(1, _def.MaxUpgradeLevel));
            if (clamped == _upgradeLevel) return;
            int previousMag = _stats.MagazineSize;
            _upgradeLevel = clamped;
            _stats = WeaponStatRules.Build(_def, _rarity, _upgradeLevel);
            // Preserve the loaded fraction so upgrading never duplicates or destroys ammo.
            float ratio = previousMag <= 0 ? 1f : (float)_ammoInMagazine / previousMag;
            _ammoInMagazine = Mathf.Clamp(Mathf.RoundToInt(ratio * _stats.MagazineSize), 0, _stats.MagazineSize);
        }

        public void SetRarity(Rarity rarity)
        {
            if (_rarity == rarity) return;
            _rarity = rarity;
            _stats = WeaponStatRules.Build(_def, _rarity, _upgradeLevel);
            _ammoInMagazine = Mathf.Min(_ammoInMagazine, _stats.MagazineSize);
        }

        /// <summary>Fresh magazine without touching the reserve pool (used when a mission starts).</summary>
        public void FillMagazine()
        {
            _ammoInMagazine = _stats.MagazineSize;
        }

        public void BeginEquip(float now)
        {
            State = WeaponState.Equipping;
            _stateStartTime = now;
            _stateEndTime = now + Mathf.Max(0.05f, _stats.EquipTime);
            EquipProgress01 = 0f;
        }

        public void Tick(float now, float deltaTime)
        {
            // Spread always recovers, even mid reload/equip, so weapons never feel stuck.
            _spread = WfMath.Damp(_spread, 0f, _def.SpreadRecovery, deltaTime);
            if (_spread < 0.001f) _spread = 0f;

            switch (State)
            {
                case WeaponState.Equipping:
                    EquipProgress01 = Progress01(now, _stateStartTime, _stateEndTime);
                    if (now >= _stateEndTime)
                    {
                        State = _ammoInMagazine > 0 ? WeaponState.Ready : WeaponState.Empty;
                        EquipProgress01 = 1f;
                    }
                    break;

                case WeaponState.Reloading:
                    ReloadProgress01 = Progress01(now, _stateStartTime, _stateEndTime);
                    if (now >= _stateEndTime)
                    {
                        State = _ammoInMagazine > 0 ? WeaponState.Ready : WeaponState.Empty;
                        ReloadProgress01 = 0f;
                    }
                    break;

                case WeaponState.Firing:
                    if (now >= _stateEndTime) State = _ammoInMagazine > 0 ? WeaponState.Ready : WeaponState.Empty;
                    break;

                case WeaponState.Empty:
                    if (_ammoInMagazine > 0) State = WeaponState.Ready;
                    break;
            }
        }

        private static float Progress01(float now, float startTime, float endTime)
        {
            float span = endTime - startTime;
            if (span <= WfMath.Epsilon) return 1f;
            return WfMath.Clamp01((now - startTime) / span);
        }

        /// <summary>
        /// Attempts a shot. reserveAmmo is read but never modified here - the owner owns the pool.
        /// Returns whether a shot happened and how much recoil/spread it produced.
        /// </summary>
        public FireAttempt TryFire(float now, float deltaTime, bool triggerHeld, bool aiming, bool moving, ref int reserveAmmo)
        {
            FireAttempt attempt;
            attempt.Fired = false;
            attempt.Shots = 0;
            attempt.SpreadDegrees = 0f;
            attempt.RecoilVertical = 0f;
            attempt.RecoilHorizontal = 0f;
            attempt.AmmoInMagazine = _ammoInMagazine;
            attempt.TriggeredReloadHint = false;

            if (State == WeaponState.Reloading || State == WeaponState.Equipping || State == WeaponState.Disabled)
            {
                return attempt;
            }

            if (_ammoInMagazine <= 0)
            {
                State = WeaponState.Empty;
                if (triggerHeld) attempt.TriggeredReloadHint = true;
                return attempt;
            }

            // triggerActive already encodes fire-mode semantics: held for Auto/Burst,
            // rising edge only for Single (resolved by WeaponRuntime).
            if (!triggerHeld) return attempt;
            if (now < _nextShotTime) return attempt;

            int shotsRequested = 1;
            if (_def.FireMode == FireMode.Burst)
            {
                if (_burstRemaining <= 0) _burstRemaining = Mathf.Max(2, _def.BurstCount);
                shotsRequested = _burstRemaining;
            }

            int shots = WfMath.ClampInt(shotsRequested, 1, _ammoInMagazine);
            float shotInterval = _stats.SecondsBetweenShots;

            for (int i = 0; i < shots; i++)
            {
                _ammoInMagazine--;
                _spread = Mathf.Min(_def.MaxSpread, _spread + _def.SpreadPerShot);

                float spreadMult = aiming ? _def.AimSpreadMultiplier : 1f;
                if (moving) spreadMult += _def.MoveSpreadPenalty * 0.5f;
                attempt.SpreadDegrees = Mathf.Max(attempt.SpreadDegrees, _stats.BaseSpread * spreadMult + _spread * spreadMult);

                float kickScale = aiming ? 0.7f : 1f;
                attempt.RecoilVertical += _stats.RecoilVertical * kickScale * (1f + _recoilShotIndex * 0.03f);
                float horizontalDir = _recoilRandom.NextFloat() < 0.5f ? -1f : 1f;
                attempt.RecoilHorizontal += _stats.RecoilHorizontal * kickScale * horizontalDir * _recoilRandom.Range(0.4f, 1f);
                _recoilShotIndex++;
            }

            if (_def.FireMode == FireMode.Burst)
            {
                _burstRemaining -= shots;
                if (_burstRemaining <= 0)
                {
                    _burstRemaining = 0;
                    // Bursts need a short rest before the next burst can start.
                    _nextShotTime = now + shotInterval * 3f;
                }
                else
                {
                    _nextShotTime = now + Mathf.Max(_def.BurstInterval, shotInterval);
                }
            }
            else
            {
                _nextShotTime = now + shotInterval;
            }

            _stateEndTime = _nextShotTime - shotInterval * 0.25f;
            if (_stateEndTime < now) _stateEndTime = now;

            attempt.Fired = true;
            attempt.Shots = shots;
            attempt.AmmoInMagazine = _ammoInMagazine;
            State = _ammoInMagazine > 0 ? WeaponState.Firing : WeaponState.Empty;
            attempt.TriggeredReloadHint = _ammoInMagazine <= 0;
            return attempt;
        }

        public void NotifyRecoilRecovered()
        {
            _recoilShotIndex = 0;
        }

        public bool CanReload()
        {
            if (State == WeaponState.Reloading || State == WeaponState.Equipping || State == WeaponState.Disabled) return false;
            return !MagazineFull;
        }

        /// <summary>Starts a reload consuming from reserveAmmo. Reserve never goes negative.</summary>
        public bool BeginReload(float now, ref int reserveAmmo)
        {
            if (!CanReload()) return false;
            if (reserveAmmo <= 0 && _ammoInMagazine > 0) return false;

            int needed = _stats.MagazineSize - _ammoInMagazine;
            int take = Mathf.Min(needed, Mathf.Max(0, reserveAmmo));
            if (take <= 0) return false;

            reserveAmmo -= take;
            _ammoInMagazine += take;
            State = WeaponState.Reloading;
            _stateStartTime = now;
            _stateEndTime = now + Mathf.Max(0.2f, _stats.ReloadTime);
            ReloadProgress01 = 0f;
            _spread = Mathf.Min(_def.MaxSpread, _spread + 0.4f);
            return true;
        }

        public void CancelReload()
        {
            if (State == WeaponState.Reloading) State = _ammoInMagazine > 0 ? WeaponState.Ready : WeaponState.Empty;
            ReloadProgress01 = 0f;
        }

        /// <summary>Refills the magazine from reserve without consuming reload time (mission start).</summary>
        public void TopUpFromReserve(ref int reserveAmmo)
        {
            int needed = _stats.MagazineSize - _ammoInMagazine;
            if (needed <= 0 || reserveAmmo <= 0) return;
            int take = Mathf.Min(needed, reserveAmmo);
            reserveAmmo -= take;
            _ammoInMagazine += take;
        }

        /// <summary>Total ammo available for this weapon (magazine + reserve) - used by HUD and save.</summary>
        public int TotalAmmo(int reserveAmmo)
        {
            return _ammoInMagazine + Mathf.Max(0, reserveAmmo);
        }
    }
}
