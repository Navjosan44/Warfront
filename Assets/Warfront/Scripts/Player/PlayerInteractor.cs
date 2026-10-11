using System.Collections.Generic;
using UnityEngine;
using Warfront.Core;

namespace Warfront.Game
{
    /// <summary>Anything the player can walk up to and use.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        float InteractRange { get; }
        Vector3 InteractPosition { get; }
        void Interact(PlayerInteractor interactor);
    }

    /// <summary>
    /// Finds the best interaction target for the player and surfaces the prompt text.
    ///
    /// It walks the mission's registered interactables (loot, objectives, extraction) instead of doing
    /// physics queries every frame: no allocations, no scene searches, and the list is authoritative.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        private GameContext _context;
        private PlayerController _player;
        private readonly List<IInteractable> _candidates = new List<IInteractable>(8);
        private IInteractable _current;
        private float _nextScan;

        public string CurrentPrompt { get; private set; }
        public IInteractable Current { get { return _current; } }

        public void Initialize(GameContext context, PlayerController player)
        {
            _context = context;
            _player = player;
        }

        private void Update()
        {
            if (_context == null || _player == null || _player.IsDead) return;
            if (_context.Flow != null && _context.Flow.IsPaused) return;

            float now = Time.time;
            if (now >= _nextScan)
            {
                _nextScan = now + 0.1f;   // ten scans a second is plenty and keeps the frame budget free
                Scan();
            }

            InputService input = _context.Input;
            if (_current != null && input != null && input.InteractPressed)
            {
                IInteractable target = _current;
                _current = null;
                CurrentPrompt = null;
                target.Interact(this);
            }
        }

        private void Scan()
        {
            _candidates.Clear();
            _current = null;
            CurrentPrompt = null;

            MissionRuntime mission = _context.Mission;
            if (mission == null) return;

            List<IInteractable> interactables = mission.Interactables;
            Vector3 playerPosition = transform.position;
            Vector3 forward = transform.forward;
            float bestScore = float.MinValue;

            for (int i = 0; i < interactables.Count; i++)
            {
                IInteractable candidate = interactables[i];
                if (candidate == null || !candidate.CanInteract) continue;

                Vector3 offset = candidate.InteractPosition - playerPosition;
                float distance = offset.magnitude;
                float range = Mathf.Max(1f, candidate.InteractRange);
                if (distance > range) continue;

                Vector3 flat = new Vector3(offset.x, 0f, offset.z);
                float facing = flat.sqrMagnitude < 0.001f ? 1f : Vector3.Dot(forward, flat.normalized);
                float score = facing * 2f - distance / range;
                if (score < bestScore) continue;

                bestScore = score;
                _current = candidate;
            }

            if (_current != null) CurrentPrompt = _current.Prompt;
        }

        public GameContext Context { get { return _context; } }
    }
}
