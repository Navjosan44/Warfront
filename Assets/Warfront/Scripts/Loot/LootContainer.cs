using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>
    /// Interactable world container. Opening it rolls from a seeded loot table and transfers the drops
    /// into the player's inventory; anything that does not fit is dropped as a pickup on the ground
    /// instead of being voided. Containers can also carry objective items (intel, supplies, comms).
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        public string ContainerId;
        public string LootTableId = "loot_standard";
        public int RollSeed;
        public float Luck;
        public int RollBonus;
        public bool Opened { get; private set; }

        // Objective mode: interacting with this container reports progress for an objective.
        public bool IsObjective;
        public ObjectiveType ObjectiveType = ObjectiveType.CollectIntel;
        public string ObjectiveTag;
        public string GrantedItemId;

        private GameContext _context;
        private Transform _lid;
        private GameObject _glow;
        private bool _highlighted;

        public string Prompt
        {
            get
            {
                if (Opened) return null;
                if (IsObjective) return "Recover supplies";
                return "Search container";
            }
        }

        public bool CanInteract { get { return !Opened; } }
        public float InteractRange { get { return 3.2f; } }
        public Vector3 InteractPosition { get { return transform.position + Vector3.up * 0.6f; } }

        public void Initialize(GameContext context, string containerId, string lootTableId, int seed, float luck, int rollBonus)
        {
            _context = context;
            ContainerId = containerId;
            LootTableId = lootTableId;
            RollSeed = seed;
            Luck = luck;
            RollBonus = rollBonus;
        }

        public void ConfigureObjective(ObjectiveType type, string tag, string grantedItemId)
        {
            IsObjective = true;
            ObjectiveType = type;
            ObjectiveTag = tag;
            GrantedItemId = grantedItemId;
        }

        public void RegisterVisuals(Transform lid, GameObject glow)
        {
            _lid = lid;
            _glow = glow;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (Opened || _context == null) return;
            Opened = true;

            if (_glow != null) _glow.SetActive(false);
            if (_lid != null) _lid.localRotation = Quaternion.Euler(-65f, 0f, 0f);

            if (IsObjective)
            {
                GrantObjectiveItems();
                return;
            }

            RollAndGrant();
        }

        private void GrantObjectiveItems()
        {
            MissionRuntime mission = _context.Mission;
            if (mission != null) mission.ReportObjective(ObjectiveType, ObjectiveTag, 1);

            if (!string.IsNullOrEmpty(GrantedItemId))
            {
                int leftover;
                InventoryModel inventory = _context.Inventory;
                InventoryResult result = inventory != null
                    ? inventory.Add(GrantedItemId, 1, Rarity.Rare, 1, out leftover)
                    : InventoryResult.InvalidItem;
                if (result == InventoryResult.Full || result == InventoryResult.PartialFill)
                {
                    SpawnPickup(GrantedItemId, 1, Rarity.Rare, 1);
                    GameEvents.RaiseNotice("Inventory full - objective item dropped nearby");
                }
            }

            if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.ObjectiveComplete);
            if (_context.Haptics != null) _context.Haptics.Objective();
            GameEvents.RaiseInventoryChanged(null, 0);
        }

        private void RollAndGrant()
        {
            LootRoller roller = new LootRoller(_context.Content, RollSeed);
            List<LootDrop> drops = new List<LootDrop>(6);
            string tableId = string.IsNullOrEmpty(LootTableId) ? "loot_standard" : LootTableId;
            roller.Roll(tableId, Luck, RollBonus, drops);

            if (drops.Count == 0 && _context.Audio != null) _context.Audio.PlayUi(AudioCue.Empty);

            InventoryModel inventory = _context.Inventory;
            for (int i = 0; i < drops.Count; i++)
            {
                LootDrop drop = drops[i];
                ItemDef item = _context.Content.GetItem(drop.ItemId);
                if (item == null) continue;

                int leftover = 0;
                InventoryResult result = inventory != null
                    ? inventory.Add(drop.ItemId, drop.Quantity, drop.Rarity, drop.UpgradeLevel, out leftover)
                    : InventoryResult.InvalidItem;

                if (result == InventoryResult.Success)
                {
                    GameEvents.RaiseLootCollected(drop.ToStack(), item.Value * drop.Quantity);
                    if (_context.Mission != null) _context.Mission.ReportLoot(drop);
                }
                else
                {
                    int amount = result == InventoryResult.Full ? drop.Quantity : leftover;
                    if (amount <= 0) continue;
                    SpawnPickup(drop.ItemId, amount, drop.Rarity, drop.UpgradeLevel);
                    GameEvents.RaiseNotice("Inventory full - " + item.DisplayName + " dropped nearby");
                }
            }

            if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Pickup);
            if (_context.Haptics != null) _context.Haptics.Objective();
            GameEvents.RaiseInventoryChanged(null, 0);
        }

        private void SpawnPickup(string itemId, int quantity, Rarity rarity, int upgradeLevel)
        {
            MissionRuntime mission = _context.Mission;
            if (mission == null) return;
            Vector3 position = transform.position + new Vector3(Random.Range(-1.4f, 1.4f), 0.3f, Random.Range(-1.4f, 1.4f));
            mission.SpawnPickup(itemId, quantity, rarity, upgradeLevel, position);
        }

        /// <summary>Builds the visual container body (called by the level / mission builder).</summary>
        public void BuildVisuals(GameAssets assets, Material accentMaterial)
        {
            GameObject body = PrimitiveFactory.CreateBox("ContainerBody", transform, new Vector3(0f, 0.45f, 0f),
                new Vector3(1.3f, 0.9f, 0.95f), assets != null ? assets.LootContainer : null, true, Quaternion.identity, GameLayers.Interactable);
            GameObject lid = PrimitiveFactory.CreateBox("ContainerLid", transform, new Vector3(0f, 0.95f, -0.5f),
                new Vector3(1.3f, 0.12f, 1.05f), assets != null ? assets.Rust : null, false, Quaternion.identity, GameLayers.Interactable);
            GameObject glow = PrimitiveFactory.CreateBox("ContainerGlow", transform, new Vector3(0f, 0.98f, 0f),
                new Vector3(0.5f, 0.06f, 0.5f), accentMaterial != null ? accentMaterial : (assets != null ? assets.ObjectiveMarker : null), false,
                Quaternion.identity, GameLayers.Interactable);
            RegisterVisuals(lid.transform, glow);
            if (body != null) body.layer = GameLayers.Interactable;
        }

        private void Update()
        {
            if (_highlighted || _glow == null || Opened) return;
            // Gentle pulse so the container reads as interactable in dim light.
            float pulse = 0.75f + Mathf.Sin(Time.time * 3f) * 0.25f;
            _glow.transform.localScale = new Vector3(0.5f * pulse, 0.06f, 0.5f * pulse);
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
        }
    }

    /// <summary>An item stack lying on the ground. Interacting tops up the inventory.</summary>
    public class WorldItemPickup : MonoBehaviour, IInteractable
    {
        public ItemStack Stack;
        public int Value;
        public int SpawnFrame;

        private GameContext _context;
        private float _bobPhase;

        public string Prompt
        {
            get
            {
                ItemDef item = _context != null ? _context.Content.GetItem(Stack.ItemId) : null;
                string name = item != null ? item.DisplayName : Stack.ItemId;
                return Stack.Quantity > 1 ? "Pick up " + name + " x" + Stack.Quantity : "Pick up " + name;
            }
        }

        public bool CanInteract { get { return Stack.Quantity > 0; } }
        public float InteractRange { get { return 2.6f; } }
        public Vector3 InteractPosition { get { return transform.position; } }

        public void Initialize(GameContext context, ItemStack stack, int value)
        {
            _context = context;
            Stack = stack;
            Value = value;
            _bobPhase = Random.Range(0f, 6.28f);
            SpawnFrame = Time.frameCount;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (_context == null || Stack.Quantity <= 0) return;

            InventoryModel inventory = _context.Inventory;
            int leftover = 0;
            InventoryResult result = inventory != null
                ? inventory.Add(Stack.ItemId, Stack.Quantity, Stack.Rarity, Stack.UpgradeLevel, out leftover)
                : InventoryResult.InvalidItem;

            if (result == InventoryResult.Full)
            {
                GameEvents.RaiseNotice("Inventory full");
                if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Deny);
                return;
            }

            int taken = Stack.Quantity - Mathf.Max(0, leftover);
            Stack.Quantity = Mathf.Max(0, leftover);

            if (taken > 0)
            {
                ItemDef item = _context.Content.GetItem(Stack.ItemId);
                GameEvents.RaiseLootCollected(new ItemStack(Stack.ItemId, taken, Stack.Rarity, Stack.UpgradeLevel),
                    (item != null ? item.Value : 0) * taken);
                if (_context.Mission != null)
                {
                    LootDrop drop;
                    drop.ItemId = Stack.ItemId;
                    drop.Quantity = taken;
                    drop.Rarity = Stack.Rarity;
                    drop.UpgradeLevel = Stack.UpgradeLevel;
                    drop.LootTableId = 0;
                    _context.Mission.ReportLoot(drop);
                }
                if (_context.Audio != null) _context.Audio.PlayUi(AudioCue.Pickup);
            }

            if (Stack.Quantity <= 0) Destroy(gameObject);
        }

        private void Update()
        {
            // Cheap bob so ground loot is visible without any particle cost.
            float y = 0.35f + Mathf.Sin(Time.time * 2.4f + _bobPhase) * 0.09f;
            transform.localPosition = new Vector3(transform.localPosition.x, y, transform.localPosition.z);
            transform.Rotate(0f, 32f * Time.deltaTime, 0f);
        }

        public void BuildVisuals(GameAssets assets)
        {
            ItemDef item = _context != null ? _context.Content.GetItem(Stack.ItemId) : null;
            Material material = assets != null && item != null
                ? assets.Lit("Pickup_" + item.Id, item.DisplayColor, 0.3f, 0.1f)
                : (assets != null ? assets.ObjectiveMarker : null);
            PrimitiveFactory.CreateBox("PickupBody", transform, Vector3.zero, new Vector3(0.34f, 0.34f, 0.34f), material, false, Quaternion.identity, GameLayers.Interactable);
        }
    }
}
