using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Procedurally built humanoid character. Limbs are primitive boxes parented to joint pivots and
    /// animated by code (walk cycle, breathing idle, aim pose, hit flinch, death fall). This keeps the
    /// project free of imported animations while still reading as a soldier rather than a capsule.
    /// </summary>
    public class CharacterRig
    {
        public GameObject RootObject;
        public Transform Hips;
        public Transform Torso;
        public Transform Chest;
        public Transform Head;
        public Transform ShoulderLeft;
        public Transform ShoulderRight;
        public Transform UpperArmLeft;
        public Transform UpperArmRight;
        public Transform ThighLeft;
        public Transform ThighRight;
        public Transform ShinLeft;
        public Transform ShinRight;
        public Transform WeaponAnchor;
        public Transform MuzzlePoint;

        public Hitbox HeadHitbox;
        public Hitbox ChestHitbox;
        public Hitbox LimbHitboxLeft;
        public Hitbox LimbHitboxRight;

        private float _walkPhase;
        private float _flinch;
        private float _deathBlend;
        private float _breathOffset;
        private float _aimBlend;
        private float _crouchBlend;
        private bool _dead;
        private float _scale = 1f;
        private Vector3 _weaponRestLocalPosition;

        public Transform AimReference { get { return Chest != null ? Chest : Torso; } }
        public bool Built { get { return RootObject != null; } }

        public void Build(Transform parent, Material bodyMaterial, Material accentMaterial, float scale, bool isPlayer, Damageable owner, bool addHitboxes)
        {
            _scale = Mathf.Max(0.4f, scale);
            _breathOffset = 0f;

            RootObject = new GameObject(isPlayer ? "PlayerRig" : "EnemyRig");
            Transform root = RootObject.transform;
            root.SetParent(parent, false);

            Material body = bodyMaterial;
            Material accent = accentMaterial;

            Hips = NewPivot("Hips", root, new Vector3(0f, 0.96f * _scale, 0f));
            Box(Hips, "Pelvis", new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.22f, 0.24f) * _scale, body);

            Torso = NewPivot("Torso", Hips, new Vector3(0f, 0.11f * _scale, 0f));
            Box(Torso, "TorsoBody", new Vector3(0f, 0.14f * _scale, 0f), new Vector3(0.44f, 0.4f, 0.26f) * _scale, body);
            Box(Torso, "Vest", new Vector3(0f, 0.15f * _scale, 0.02f * _scale), new Vector3(0.46f, 0.3f, 0.3f) * _scale, accent);

            Chest = NewPivot("Chest", Torso, new Vector3(0f, 0.34f * _scale, 0f));
            Box(Chest, "ChestBody", new Vector3(0f, 0.03f * _scale, 0f), new Vector3(0.46f, 0.2f, 0.26f) * _scale, body);

            Head = NewPivot("Head", Chest, new Vector3(0f, 0.2f * _scale, 0f));
            Box(Head, "Skull", new Vector3(0f, 0.1f * _scale, 0f), new Vector3(0.22f, 0.24f, 0.22f) * _scale, body);
            Box(Head, "Visor", new Vector3(0f, 0.1f * _scale, 0.1f * _scale), new Vector3(0.24f, 0.09f, 0.06f) * _scale, accent);

            ShoulderLeft = NewPivot("ShoulderLeft", Chest, new Vector3(-0.27f * _scale, 0.06f * _scale, 0f));
            ShoulderRight = NewPivot("ShoulderRight", Chest, new Vector3(0.27f * _scale, 0.06f * _scale, 0f));
            UpperArmLeft = NewPivot("UpperArmLeft", ShoulderLeft, Vector3.zero);
            UpperArmRight = NewPivot("UpperArmRight", ShoulderRight, Vector3.zero);
            Box(UpperArmLeft, "ArmL", new Vector3(0f, -0.16f * _scale, 0f), new Vector3(0.12f, 0.32f, 0.12f) * _scale, body);
            Box(UpperArmRight, "ArmR", new Vector3(0f, -0.16f * _scale, 0f), new Vector3(0.12f, 0.32f, 0.12f) * _scale, body);

            Transform forearmLeft = NewPivot("ForearmLeft", UpperArmLeft, new Vector3(0f, -0.32f * _scale, 0f));
            Transform forearmRight = NewPivot("ForearmRight", UpperArmRight, new Vector3(0f, -0.32f * _scale, 0f));
            Box(forearmLeft, "ForearmL", new Vector3(0f, -0.14f * _scale, 0f), new Vector3(0.1f, 0.28f, 0.1f) * _scale, body);
            Box(forearmRight, "ForearmR", new Vector3(0f, -0.14f * _scale, 0f), new Vector3(0.1f, 0.28f, 0.1f) * _scale, body);

            WeaponAnchor = NewPivot("WeaponAnchor", forearmRight, new Vector3(0f, -0.28f * _scale, 0.1f * _scale));
            _weaponRestLocalPosition = WeaponAnchor.localPosition;

            ThighLeft = NewPivot("ThighLeft", Hips, new Vector3(-0.12f * _scale, -0.08f * _scale, 0f));
            ThighRight = NewPivot("ThighRight", Hips, new Vector3(0.12f * _scale, -0.08f * _scale, 0f));
            Box(ThighLeft, "ThighL", new Vector3(0f, -0.2f * _scale, 0f), new Vector3(0.15f, 0.4f, 0.15f) * _scale, body);
            Box(ThighRight, "ThighR", new Vector3(0f, -0.2f * _scale, 0f), new Vector3(0.15f, 0.4f, 0.15f) * _scale, body);

            ShinLeft = NewPivot("ShinLeft", ThighLeft, new Vector3(0f, -0.4f * _scale, 0f));
            ShinRight = NewPivot("ShinRight", ThighRight, new Vector3(0f, -0.4f * _scale, 0f));
            Box(ShinLeft, "ShinL", new Vector3(0f, -0.2f * _scale, 0f), new Vector3(0.13f, 0.4f, 0.13f) * _scale, body);
            Box(ShinRight, "ShinR", new Vector3(0f, -0.2f * _scale, 0f), new Vector3(0.13f, 0.4f, 0.13f) * _scale, body);
            Box(ShinLeft, "BootL", new Vector3(0f, -0.42f * _scale, 0.05f * _scale), new Vector3(0.14f, 0.1f, 0.26f) * _scale, accent);
            Box(ShinRight, "BootR", new Vector3(0f, -0.42f * _scale, 0.05f * _scale), new Vector3(0.14f, 0.1f, 0.26f) * _scale, accent);

            if (addHitboxes && owner != null)
            {
                HeadHitbox = AddHitbox("HeadHitbox", Head, new Vector3(0f, 0.1f * _scale, 0f), new Vector3(0.26f, 0.28f, 0.26f) * _scale, owner, HitZone.Head);
                ChestHitbox = AddHitbox("ChestHitbox", Chest, new Vector3(0f, 0.02f * _scale, 0f), new Vector3(0.52f, 0.5f, 0.34f) * _scale, owner, HitZone.Body);
                LimbHitboxLeft = AddHitbox("LimbLeftHitbox", ThighLeft, new Vector3(0f, -0.4f * _scale, 0f), new Vector3(0.22f, 0.8f, 0.22f) * _scale, owner, HitZone.Limbs);
                LimbHitboxRight = AddHitbox("LimbRightHitbox", ThighRight, new Vector3(0f, -0.4f * _scale, 0f), new Vector3(0.22f, 0.8f, 0.22f) * _scale, owner, HitZone.Limbs);
            }

            PrimitiveFactory.SetLayerRecursive(RootObject, isPlayer ? GameLayers.Player : GameLayers.Enemy);
            if (addHitboxes)
            {
                SetHitboxLayer(HeadHitbox);
                SetHitboxLayer(ChestHitbox);
                SetHitboxLayer(LimbHitboxLeft);
                SetHitboxLayer(LimbHitboxRight);
            }
        }

        private static void SetHitboxLayer(Hitbox hitbox)
        {
            if (hitbox == null) return;
            hitbox.gameObject.layer = GameLayers.Hitbox;
        }

        private static Transform NewPivot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject pivot = new GameObject(name);
            Transform t = pivot.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            return t;
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            return PrimitiveFactory.CreateBox(name, parent, localPosition, size, material, false, Quaternion.identity, parent.gameObject.layer);
        }

        private static Hitbox AddHitbox(string name, Transform parent, Vector3 localPosition, Vector3 size, Damageable owner, HitZone zone)
        {
            GameObject go = new GameObject(name);
            go.layer = GameLayers.Hitbox;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            Hitbox hitbox = go.AddComponent<Hitbox>();
            float multiplier = zone == HitZone.Head ? 1f : zone == HitZone.Limbs ? 0.75f : 1f;
            hitbox.Configure(owner, zone, multiplier);
            return hitbox;
        }

        /// <summary>Attaches a built weapon model and returns the muzzle transform for effects.</summary>
        public Transform AttachWeapon(GameObject weaponModel)
        {
            if (weaponModel == null || WeaponAnchor == null) return null;
            weaponModel.transform.SetParent(WeaponAnchor, false);
            weaponModel.transform.localPosition = Vector3.zero;
            weaponModel.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            Transform muzzle = WeaponBuilder.FindMuzzle(weaponModel.transform);
            MuzzlePoint = muzzle != null ? muzzle : weaponModel.transform;
            return MuzzlePoint;
        }

        public void SetWeaponVisible(bool visible)
        {
            if (WeaponAnchor == null || WeaponAnchor.childCount == 0) return;
            WeaponAnchor.GetChild(0).gameObject.SetActive(visible);
        }

        /// <summary>Drives the whole rig. Called once per frame by the owning controller.</summary>
        public void Tick(float deltaTime, float speed01, bool aiming, float aimPitchDegrees, bool crouching, bool grounded)
        {
            if (RootObject == null) return;

            if (_dead)
            {
                TickDeath(deltaTime);
                return;
            }

            float dt = Mathf.Max(0f, deltaTime);
            speed01 = Mathf.Clamp01(speed01);
            _aimBlend = Mathf.MoveTowards(_aimBlend, aiming ? 1f : 0f, dt * 6f);
            _crouchBlend = Mathf.MoveTowards(_crouchBlend, crouching ? 1f : 0f, dt * 6f);

            // Walk cycle: the phase advances with actual ground speed so feet match movement.
            _walkPhase += dt * (2.4f + speed01 * 7.5f) * (speed01 > 0.02f ? 1f : 0f);
            float swing = Mathf.Sin(_walkPhase) * (12f + speed01 * 34f);

            ThighLeft.localRotation = Quaternion.Euler(swing, 0f, 0f);
            ThighRight.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            ShinLeft.localRotation = Quaternion.Euler(Mathf.Max(0f, -swing) * 0.9f, 0f, 0f);
            ShinRight.localRotation = Quaternion.Euler(Mathf.Max(0f, swing) * 0.9f, 0f, 0f);

            // Breathing idle plus a small bob while running.
            float breath = Mathf.Sin(Time.time * 1.6f + _breathOffset) * 1.6f;
            float bob = Mathf.Abs(Mathf.Sin(_walkPhase)) * 3.2f * speed01;
            Hips.localPosition = new Vector3(0f, (0.96f - _crouchBlend * 0.28f) * _scale, 0f);
            Hips.localRotation = Quaternion.Euler(bob * 0.4f, Mathf.Sin(_walkPhase) * 3f * speed01, 0f);

            float torsoPitch = -aimPitchDegrees * 0.35f * _aimBlend - _crouchBlend * 12f + breath * 0.4f;
            Torso.localRotation = Quaternion.Euler(torsoPitch, Mathf.Sin(_walkPhase * 0.5f) * 2f, 0f);
            Chest.localRotation = Quaternion.Euler(-aimPitchDegrees * 0.25f * _aimBlend, 0f, 0f);
            Head.localRotation = Quaternion.Euler(-aimPitchDegrees * 0.4f * _aimBlend + breath * 0.2f, Mathf.Sin(Time.time * 0.7f + _breathOffset) * 4f * (1f - _aimBlend), 0f);

            // Arms: counter swing when moving, locked to the weapon when aiming.
            float armSwing = Mathf.Sin(_walkPhase + Mathf.PI) * (8f + speed01 * 20f) * (1f - _aimBlend);
            UpperArmLeft.localRotation = Quaternion.Euler(armSwing - _aimBlend * 62f, 0f, 12f + _aimBlend * 18f);
            UpperArmRight.localRotation = Quaternion.Euler(-armSwing * 0.4f - _aimBlend * 74f, 0f, -8f - _aimBlend * 8f);

            if (WeaponAnchor != null)
            {
                WeaponAnchor.localPosition = Vector3.Lerp(_weaponRestLocalPosition, _weaponRestLocalPosition + new Vector3(-0.06f, 0.05f, 0.16f) * _scale, _aimBlend);
            }

            if (_flinch > 0f)
            {
                _flinch = Mathf.Max(0f, _flinch - dt * 2.6f);
                float power = _flinch * 9f;
                Torso.localRotation *= Quaternion.Euler(-power, Mathf.Sin(_flinch * 40f) * power * 0.4f, 0f);
                Head.localRotation *= Quaternion.Euler(-power * 0.6f, 0f, 0f);
            }

            if (!grounded) { }
        }

        public void PlayHitReaction(float strength)
        {
            _flinch = Mathf.Clamp01(Mathf.Max(_flinch, 0.35f + strength * 0.65f));
        }

        public void PlayDeath()
        {
            _dead = true;
            _deathBlend = 0f;
        }

        private void TickDeath(float deltaTime)
        {
            _deathBlend = Mathf.Min(1f, _deathBlend + deltaTime * 2.4f);
            float fall = Mathf.SmoothStep(0f, 88f, _deathBlend);
            RootObject.transform.localRotation = Quaternion.Euler(0f, 0f, fall);
            if (Hips != null)
            {
                Hips.localPosition = new Vector3(0f, Mathf.Lerp((0.96f - _crouchBlend * 0.28f) * _scale, 0.24f * _scale, _deathBlend), 0f);
            }
            if (Torso != null) Torso.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -14f, _deathBlend), 0f, 0f);
            if (UpperArmLeft != null) UpperArmLeft.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -30f, _deathBlend), 0f, 40f * _deathBlend);
            if (UpperArmRight != null) UpperArmRight.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -20f, _deathBlend), 0f, -50f * _deathBlend);
            if (ShinLeft != null) ShinLeft.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 55f, _deathBlend), 0f, 0f);
            if (ShinRight != null) ShinRight.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 40f, _deathBlend), 0f, 0f);
        }

        public void ResetPose()
        {
            _dead = false;
            _deathBlend = 0f;
            _flinch = 0f;
            _aimBlend = 0f;
            _crouchBlend = 0f;
            _walkPhase = 0f;
            if (RootObject != null) RootObject.transform.localRotation = Quaternion.identity;
            if (Hips != null) Hips.localPosition = new Vector3(0f, 0.96f * _scale, 0f);
        }

        /// <summary>Disables hitboxes (used when an enemy dies so corpses are not shootable).</summary>
        public void SetHitboxesEnabled(bool enabled)
        {
            Hitbox[] hitboxes = { HeadHitbox, ChestHitbox, LimbHitboxLeft, LimbHitboxRight };
            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null) hitboxes[i].gameObject.SetActive(enabled);
            }
        }

        public void SetVisible(bool visible)
        {
            if (RootObject != null) RootObject.SetActive(visible);
        }
    }
}
