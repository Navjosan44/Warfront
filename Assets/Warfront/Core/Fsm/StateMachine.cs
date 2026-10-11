using System;
using System.Collections.Generic;

namespace Warfront.Core
{
    /// <summary>Minimal allocation-free finite state machine used by AI, mission and app flow.</summary>
    public class StateMachine<TContext>
    {
        public interface IState
        {
            string Name { get; }
            void Enter(TContext context);
            void Tick(TContext context, float deltaTime);
            void Exit(TContext context);
        }

        private readonly Dictionary<string, IState> _states = new Dictionary<string, IState>(16);
        private readonly TContext _context;
        private IState _current;
        private float _timeInState;

        public StateMachine(TContext context)
        {
            _context = context;
        }

        public string CurrentName { get { return _current == null ? "None" : _current.Name; } }
        public float TimeInState { get { return _timeInState; } }

        public void Add(IState state)
        {
            if (state == null || string.IsNullOrEmpty(state.Name)) return;
            _states[state.Name] = state;
        }

        public bool Has(string name)
        {
            return !string.IsNullOrEmpty(name) && _states.ContainsKey(name);
        }

        public void Change(string name)
        {
            IState next;
            if (string.IsNullOrEmpty(name) || !_states.TryGetValue(name, out next))
            {
                return;
            }
            if (ReferenceEquals(next, _current)) return;

            if (_current != null) _current.Exit(_context);
            _current = next;
            _timeInState = 0f;
            _current.Enter(_context);
        }

        public void Tick(float deltaTime)
        {
            if (_current == null) return;
            _timeInState += deltaTime;
            _current.Tick(_context, deltaTime);
        }

        public void Stop()
        {
            if (_current != null) _current.Exit(_context);
            _current = null;
            _timeInState = 0f;
        }
    }

    /// <summary>Base class that removes the boilerplate from state implementations.</summary>
    public abstract class WfState<TContext> : StateMachine<TContext>.IState
    {
        private readonly string _name;

        protected WfState(string name)
        {
            _name = name;
        }

        public string Name { get { return _name; } }

        public virtual void Enter(TContext context) { }
        public virtual void Tick(TContext context, float deltaTime) { }
        public virtual void Exit(TContext context) { }
    }

    /// <summary>Delegate based state: keeps one-file AI states readable without new classes.</summary>
    public class ActionState<TContext> : StateMachine<TContext>.IState
    {
        private readonly Action<TContext> _enter;
        private readonly Action<TContext, float> _tick;
        private readonly Action<TContext> _exit;

        public ActionState(string name, Action<TContext> enter, Action<TContext, float> tick, Action<TContext> exit)
        {
            Name = name;
            _enter = enter;
            _tick = tick;
            _exit = exit;
        }

        public string Name { get; private set; }

        public void Enter(TContext context) { if (_enter != null) _enter(context); }
        public void Tick(TContext context, float deltaTime) { if (_tick != null) _tick(context, deltaTime); }
        public void Exit(TContext context) { if (_exit != null) _exit(context); }
    }
}
