﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace StateMechanic
{
    /// <summary>
    /// A state machine, which may exist as a child state machine
    /// </summary>
    public class ChildStateMachine<TState> : IStateMachine, IEventDelegate, IStateDelegate<TState>
        where TState : StateBase<TState>, new()
    {
        internal StateMachineKernel<TState> Kernel { get; }

        private readonly List<TState> states = new List<TState>();

        /// <summary>
        /// Gets the parent state of this state machine, or null if there is none
        /// </summary>
        public TState ParentState { get; }

        /// <summary>
        /// Gets the initial state of this state machine
        /// </summary>
        public TState InitialState { get; private set; }

        private TState _currentState;

        /// <summary>
        /// Gets the state which this state machine is currently in
        /// </summary>
        public TState CurrentState
        {
            get
            {
                if (this.Kernel.Fault != null)
                    throw new StateMachineFaultedException(this.Kernel.Fault);

                return this._currentState;
            }
            private set
            {
                this._currentState = value;
            }
        }

        /// <summary>
        /// If <see cref="CurrentState"/> has a child state machine, gets that child state machine's current state (recursively), otherwise gets <see cref="CurrentState"/>
        /// </summary>
        public TState CurrentChildState
        {
            get
            {
                if (this.CurrentState != null && this.CurrentState.ChildStateMachine != null)
                    return this.CurrentState.ChildStateMachine.CurrentChildState;
                else
                    return this.CurrentState;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current state machine is active
        /// </summary>
        public bool IsActive => this.CurrentState != null;

        /// <summary>
        /// Gets the name given to this state machine when it was created
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the parent of this state machine, or null if there is none
        /// </summary>
        public IStateMachine ParentStateMachine => this.ParentState?.ParentStateMachine;

        internal ChildStateMachine<TState> TopmostStateMachineInternal => this.ParentState?.ParentStateMachine.TopmostStateMachineInternal ?? this;

        /// <summary>
        /// Gets the top-most state machine in this state machine hierarchy (which may be 'this')
        /// </summary>
        public IStateMachine TopmostStateMachine => this.TopmostStateMachineInternal;

        IState IStateMachine.ParentState => this.ParentState;
        IState IStateMachine.CurrentState => this.CurrentState;
        IState IStateMachine.CurrentChildState => this.CurrentChildState;
        IState IStateMachine.InitialState => this.InitialState;
        IStateMachine IStateMachine.ParentStateMachine => this.ParentStateMachine;
        IStateMachine IStateMachine.TopmostStateMachine => this.TopmostStateMachineInternal;
        IEventDelegate IEventDelegate.TopmostStateMachine => this.TopmostStateMachineInternal;

        /// <summary>
        /// Gets a list of all states which are part of this state machine
        /// </summary>
        public IReadOnlyList<TState> States { get; }

        IReadOnlyList<IState> IStateMachine.States => this.States;

        internal ChildStateMachine(string name, StateMachineKernel<TState> kernel, TState parentState)
        {
            this.Name = name;
            this.Kernel = kernel;
            this.ParentState = parentState;

            this.States = new ReadOnlyCollection<TState>(this.states);
        }

        /// <summary>
        /// Create a new state and add it to this state machine
        /// </summary>
        /// <param name="name">Name given to the state</param>
        /// <returns>The new state</returns>
        public TState CreateState(string name = null)
        {
            return this.CreateState<TState>(name);
        }

        /// <summary>
        /// Create a new state (with a custom type) and add it to this state machine
        /// </summary>
        /// <param name="name">Name given to the state</param>
        /// <returns>The new state</returns>
        public TNewState CreateState<TNewState>(string name = null) where TNewState : TState, new()
        {
            var state = new TNewState();
            state.Initialize(name, this);
            this.states.Add(state);
            return state;
        }

        /// <summary>
        /// Create the state which this state machine will be in when it first starts. This must be called exactly once per state machine
        /// </summary>
        /// <param name="name">Name given to the state</param>
        /// <returns>The new state</returns>
        public TState CreateInitialState(string name = null)
        {
            return this.CreateInitialState<TState>(name);
        }

        /// <summary>
        /// Create the state (with a custom type) which this state machine will be in when it first starts. This must be called exactly once per state machine
        /// </summary>
        /// <param name="name">Name given to the state</param>
        /// <returns>The new state</returns>
        public TNewState CreateInitialState<TNewState>(string name = null) where TNewState : TState, new()
        {
            var state = this.CreateState<TNewState>(name);
            this.SetInitialState(state);
            return state;
        }

        private void SetInitialState(TState state)
        {
            if (this.InitialState != null)
                throw new InvalidOperationException("Initial state has already been set");

            this.InitialState = state;

            // Child state machines start off in no state, and progress to the initial state
            // Normal state machines start in the start state
            // The exception is child state machines which are children of their parent's initial state, where the parent is not a child state machine

            this.ResetCurrentState();
        }

        private void ResetCurrentState()
        {
            if (this.ParentState == null || this.ParentState == this.ParentState.ParentStateMachine.CurrentState)
                this.CurrentState = this.InitialState;
            else
                this.CurrentState = null;
        }

        // Only supposed to be called from subclasses
        internal bool InvokeTransition(Func<ITransitionInvoker<TState>, bool> method, ITransitionInvoker<TState> transitionInvoker)
        {
            if (this.Kernel.Fault != null)
                throw new StateMachineFaultedException(this.Kernel.Fault);

            if (this.Kernel.ExecutingTransition)
            {
                this.Kernel.EnqueueTransition(method, transitionInvoker);
                return true;
            }

            bool success;

            try
            {
                this.Kernel.ExecutingTransition = true;
                success = method(transitionInvoker);
            }
            catch (InternalTransitionFaultException e)
            {
                var faultInfo = new StateMachineFaultInfo(this, e.FaultedComponent, e.InnerException, e.From, e.To, e.Event, e.Group);
                this.Kernel.SetFault(faultInfo);
                throw new TransitionFailedException(faultInfo);
            }
            finally
            {
                this.Kernel.ExecutingTransition = false;
            }

            this.Kernel.FireQueuedTransitions();

            return success;
        }

        /// <summary>
        /// Determines whether this state machine is a child of another state machine
        /// </summary>
        /// <param name="parentStateMachine">State machine which may be a parent of this state machine</param>
        /// <returns>True if this state machine is a child of the given state machine</returns>
        public bool IsChildOf(IStateMachine parentStateMachine)
        {
            if (parentStateMachine == null)
                throw new ArgumentNullException(nameof(parentStateMachine));

            if (this.ParentStateMachine != null)
                return this.ParentStateMachine == parentStateMachine || this.ParentStateMachine.IsChildOf(parentStateMachine);

            return false;
        }

        /// <summary>
        /// Resets the state machine, removing any fault and returning it and any child state machines to their initial state
        /// </summary>
        public void Reset()
        {
            if (this.Kernel.Synchronizer != null)
                this.Kernel.Synchronizer.Reset(this.ResetInternal);
            else
                this.ResetInternal();
        }

        private void ResetInternal()
        {
            this.Kernel.Reset();
            this.ResetChildStateMachine();
        }

        internal void ResetChildStateMachine()
        {
            // We need to reset our current state before resetting any child state machines, as the
            // child state machine's current state depends on whether or not we're active

            this.ResetCurrentState();

            foreach (var state in this.states)
            {
                state.Reset();
            }
        }

        private void HandleTransitionNotFound(IEvent @event, bool throwException)
        {
            this.Kernel.HandleTransitionNotFound(this.CurrentState, @event, this, throwException);

            if (throwException)
                throw new TransitionNotFoundException(this.CurrentState, @event, this);
        }

        internal void SetCurrentState(TState state)
        {
            if (state != null && state.ParentStateMachine != this)
                throw new InvalidOperationException($"Cannot set current state of {this} to {state}, as that state does not belong to that state machine");

            this.CurrentState = state;
        }

        bool IEventDelegate.RequestEventFireFromEvent(Event @event, EventFireMethod eventFireMethod)
        {
            var transitionInvoker = new EventTransitionInvoker<TState>(@event, eventFireMethod);
            return this.RequestEventFireFromEvent(transitionInvoker);
        }

        bool IEventDelegate.RequestEventFireFromEvent<TEventData>(Event<TEventData> @event, TEventData eventData, EventFireMethod eventFireMethod)
        {
            var transitionInvoker = new EventTransitionInvoker<TState, TEventData>(@event, eventFireMethod, eventData);
            return this.RequestEventFireFromEvent(transitionInvoker);
        }

        // invoker: Action which actually triggers the transition. Takes the state to transition from, and returns whether the transition was found
        private bool RequestEventFireFromEvent(ITransitionInvoker<TState> transitionInvoker)
        {
            if (this.Kernel.Synchronizer != null)
                return this.Kernel.Synchronizer.FireEvent(() => this.InvokeTransition(this.RequestEventFire, transitionInvoker), transitionInvoker.EventFireMethod);
            else
                return this.InvokeTransition(this.RequestEventFire, transitionInvoker);
        }

        private bool RequestEventFire(ITransitionInvoker<TState> transitionInvoker)
        {
            return this.RequestEventFire(transitionInvoker, overrideNoThrow: false);
        }

        private bool RequestEventFire(ITransitionInvoker<TState> transitionInvoker, bool overrideNoThrow)
        {
            this.EnsureCurrentStateSuitableForTransition();

            bool success;

            // Try and fire it on the child state machine - see if that works
            // If we got to here, this.CurrentState != null
            var childStateMachine = this.CurrentState.ChildStateMachine;
            if (childStateMachine != null && childStateMachine.RequestEventFire(transitionInvoker, overrideNoThrow: true))
            {
                success = true;
            }
            else
            {
                // No? Invoke it on ourselves
                success = transitionInvoker.TryInvoke(this.CurrentState);

                if (!success)
                    this.HandleTransitionNotFound(transitionInvoker.Event, throwException: !overrideNoThrow && transitionInvoker.EventFireMethod == EventFireMethod.Fire);
            }

            return success;
        }

        private void EnsureCurrentStateSuitableForTransition()
        {
            if (this.CurrentState == null)
            {
                if (this.InitialState == null)
                    throw new InvalidOperationException("Initial state not yet set. You must call CreateInitialState");
                else
                    throw new InvalidOperationException("Child state machine's parent state is not current. This state machine is currently disabled");
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object</returns>
        public override string ToString()
        {
            var parentName = (this.ParentStateMachine == null) ? "" : $" Parent={this.ParentStateMachine.Name ?? "(unnamed)"}";
            var stateName = (this.CurrentState == null) ? "None" : (this.CurrentState.Name ?? "(unnamed)");
            return $"<StateMachine{parentName} Name={this.Name ?? "(unnamed)"} State={stateName}>";
        }
    }
}
