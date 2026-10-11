using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Scene behaviour of one weapon. It owns no balance data: everything comes from the pure
    /// WeaponModel in Core, and this class only translates intent into effects, hits and feedback.
    /// Player fire is hitscan with tracers; enemies use visible pooled projectiles (see Projectile).
    /// </summary>
    public class WeaponRuntime : MonoBehaviour
    {
        public GameObject ModelObject;
        public WeaponDef Def;
        public bool IsEquipped { get; private set; }

        private WeaponModel _model;
        private GameContext _context;
        private PlayerLoadout _loadout;
        private WfRandom _critRandom;
        private float _time;
        private float _adsBlend;
        private float _emptyClickCooldown;
        private Vector3 _hipLocalPosition;
        private Vector3 _adsLocalPosition;
        private bool _wasReloading;

        public WeaponModel Model { get { return _model; } }

        public void Initialize(GameContext context, PlayerLoadout loadout, GameObject modelObject, WeaponDef def, WeaponModel model)
        {
            _context = context;
            _loadout = loadout;
            ModelObject = modelObject;
            Def = def;
            _model = model;
            _critRandom = new WfRandom(WfMath.Hash(def != null ? def.Id : "weapon") + 5077);
            _hipLocalPosition = ModelObject != null ? ModelObject.transform.localPosition : Vector3.zero;
            _adsLocalPosition = _hipLocalPosition + new Vector3(-0.04f, 0.035f, 0.1f);
            SetEquipped(false, true);
        }

        public void SetEquipped(bool equipped, bool animate)
        {
            IsEquipped = equipped;
            if (ModelObject != null) ModelObject.SetActive(equipped);
            if (equipped && animate && _model != null) _model.BeginEquip(_time);
        }

        private void Update()
        {
            if (_context == null || Def == null || _model == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            bool paused = _context.Flow != null && _context.Flow.IsPaused;
            bool alive = _context.Player == null || !_context.Player.IsDead;

            if (!IsEquipped)
            {
                _model.Tick(_time, dt);
                _time += dt;
                return;
            }

            _time += dt;
            _model.Tick(_time, dt);

            InputService input = _context.Input;
            bool aiming = !paused && alive && input != null && input.AimHeld;
            bool moving = _context.Player != null && _context.Player.Speed > 1.2f;
            _adsBlend = Mathf.MoveTowards(_adsBlend, aiming ? 1f : 0f, dt * Mathf.Max(4f, Def.AdsSpeed));

            if (ModelObject != null)
            {
                Transform t = ModelObject.transform;
                t.localPosition = Vector3.Lerp(_hipLocalPosition, _adsLocalPosition, _adsBlend);
            }

            if (paused || !alive || input == null)
            {
                return;
            }

            bool triggerActive = Def.FireMode == FireMode.Auto ? input.FireHeld : input.FirePressed;

            if (input.ReloadPressed) BeginReload();
            if (_wasReloading && _model.State != WeaponState.Reloading)
            {
                _wasReloading = false;
                GameEvents.RaiseWeaponReloadFinished();
            }

            int reserve = _loadout != null ? _loadout.ReserveAmmo(Def.AmmoType) : 0;
            FireAttempt attempt = _model.TryFire(_time, dt, triggerActive, aiming, moving, ref reserve);

            if (attempt.Fired)
            {
                ResolveShots(attempt, aiming);
            }
            else if (attempt.TriggeredReloadHint && _emptyClickCooldown <= 0f)
            {
                _emptyClickCooldown = 0.35f;
                if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Empty);
                // Auto reload keeps mobile play smooth: an empty weapon never stays empty by accident.
                BeginReload();
            }

            _emptyClickCooldown = Mathf.Max(0f, _emptyClickCooldown - dt);
        }

        private void BeginReload()
        {
            if (_loadout == null || _model == null) return;
            if (_model.State == WeaponState.Reloading) return;
            if (_model.MagazineFull) return;

            if (_loadout.TryReloadActive(_time, true))
            {
                _wasReloading = true;
                if (_context.Audio != null) _context.Audio.PlayReload(Def, transform.position);
                GameEvents.RaiseWeaponReloadStarted();
            }
        }

        private void ResolveShots(FireAttempt attempt, bool aiming)
        {
            PlayerCamera playerCamera = _context != null ? _context.Camera : null;
            Ray aimRay = playerCamera != null
                ? playerCamera.AimRay
                : new Ray(transform.position, transform.forward);

            Transform muzzle = _context != null && _context.Player != null && _context.Player.Rig != null
                ? _context.Player.Rig.MuzzlePoint
                : null;
            Vector3 muzzlePosition = muzzle != null ? muzzle.position : transform.position;

            WeaponStats stats = _model.Stats;
            for (int i = 0; i < attempt.Shots; i++)
            {
                FireOnePellet(stats, aimRay, muzzlePosition, attempt.SpreadDegrees, aiming);
            }

            if (_context.Audio != null) _context.Audio.PlayWeaponShot(Def, muzzlePosition);
            if (_context.Vfx != null) _context.Vfx.SpawnMuzzleFlash(muzzlePosition, muzzle != null ? muzzle.rotation : transform.rotation, 1f);

            if (playerCamera != null) playerCamera.AddRecoil(attempt.RecoilVertical, attempt.RecoilHorizontal);
            if (_context.Haptics != null) _context.Haptics.Fire();

            GameEvents.RaiseWeaponFired();
            GameEvents.RaiseNoise(muzzlePosition);
        }

        private void FireOnePellet(WeaponStats stats, Ray aimRay, Vector3 muzzlePosition, float spreadDegrees, bool aiming)
        {
            Vector3 direction = aimRay.direction;
            if (spreadDegrees > 0.001f)
            {
                float yaw = _critRandom.Range(-spreadDegrees, spreadDegrees);
                float pitch = _critRandom.Range(-spreadDegrees, spreadDegrees);
                direction = Quaternion.Euler(pitch, yaw, 0f) * direction;
            }

            float range = stats.Range > 0f ? stats.Range : Def.Range;
            RaycastHit hit;
            Vector3 endPoint = aimRay.origin + direction * range;
            bool didHit = Physics.Raycast(aimRay.origin, direction, out hit, range, GameLayers.ShootableMask, QueryTriggerInteraction.Collide);

            Damageable damageable = null;
            HitZone zone = HitZone.Body;
            if (didHit)
            {
                endPoint = hit.point;

                DamageRequest request = BuildDamageRequest(stats, hit.distance);
                float critRoll = _critRandom.NextFloat();
                DamageResult result = Damageable.ResolveHit(hit, request, critRoll, out zone, out damageable);

                if (damageable != null)
                {
                    bool isEnemy = damageable.TeamId != 0;
                    if (isEnemy)
                    {
                        if (_context.Vfx != null) _context.Vfx.SpawnBlood(hit.point);
                        GameEvents.RaiseHitConfirmed(result.Killed);
                        if (_context.Audio != null) _context.Audio.PlayImpact(hit.point, true);
                        if (_context.Haptics != null) _context.Haptics.HitConfirm();
                    }
                    else
                    {
                        if (_context.Vfx != null) _context.Vfx.SpawnImpact(hit.point, hit.normal, 0.7f);
                    }
                }
                else
                {
                    if (_context.Vfx != null) _context.Vfx.SpawnImpact(hit.point, hit.normal, 1f);
                    if (_context.Audio != null) _context.Audio.PlayImpact(hit.point, false);
                }
            }

            if (_context.Vfx != null) _context.Vfx.SpawnTracer(muzzlePosition, endPoint, 1f);
        }

        private DamageRequest BuildDamageRequest(WeaponStats stats, float distance)
        {
            DamageRequest request = new DamageRequest();
            request.BaseDamage = stats.Damage;
            request.Zone = HitZone.Body;
            request.Source = DamageSource.Gunfire;
            request.Distance = distance;
            request.FalloffStart = stats.FalloffStart;
            request.FalloffEnd = stats.FalloffEnd;
            request.MinFalloffMultiplier = stats.MinFalloffMultiplier;
            request.HeadshotMultiplier = stats.HeadshotMultiplier;
            request.CritChance = stats.CritChance;
            request.CritMultiplier = stats.CritMultiplier;
            request.ArmorPenetration = stats.ArmorPenetration;
            request.ShooterId = 0;
            return request;
        }
    }
}
