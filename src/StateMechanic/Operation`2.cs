﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StateMechanic
{
    /// <summary>
    /// An operation is a collection of a state which represents an operation which takes
    /// time to complete, and event which can trigger a transition to that state, and a concept
    /// a state transitioned to when the operation completes, or when it fails.
    /// </summary>
    /// <typeparam name="TState">Type of state</typeparam>
    /// <typeparam name="TEventData">Type of event data</typeparam>
    public class Operation<TState, TEventData> where TState : StateBase<TState>, new()
    {
        private readonly OperationInner<TState, Event<TEventData>> operationInner;

        /// <summary>
        /// Event which can trigger a transition to the <see cref="OperationStates"/>
        /// </summary>
        public Event<TEventData> Event => this.operationInner.Event;

        /// <summary>
        /// States which represents the operation being executed
        /// </summary>
        public IReadOnlyList<TState> OperationStates => this.operationInner.OperationStates;

        /// <summary>
        /// A collection of states which are transitioned to from <see cref="OperationStates"/>
        /// when the operation completes successfully
        /// </summary>
        public IReadOnlyList<TState> SuccessStates => this.operationInner.SuccessStates;

        /// <summary>
        /// Instantiates a new instance of the <see cref="Operation{TState}"/> class
        /// </summary>
        /// <param name="event">Event which can trigger a transition to the operationState</param>
        /// <param name="operationState">State which represents the in-progress operation</param>
        /// <param name="successStates">States which can be transitioned to from the operationState which indicate a successful operation</param>
        public Operation(Event<TEventData> @event, TState operationState, params TState[] successStates)
        {
            this.operationInner = new OperationInner<TState, Event<TEventData>>(@event, new ReadOnlyCollection<TState>(new[] { operationState }), new ReadOnlyCollection<TState>(successStates));
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="Operation{TState}"/> class
        /// </summary>
        /// <param name="event">Event which can trigger a transition to the operationState</param>
        /// <param name="operationStates">State which represent the in-progress operation</param>
        /// <param name="successStates">States which can be transitioned to from the operationState which indicate a successful operation</param>
        public Operation(Event<TEventData> @event, IEnumerable<TState> operationStates, IEnumerable<TState> successStates)
        {
            this.operationInner = new OperationInner<TState, Event<TEventData>>(@event, new ReadOnlyCollection<TState>(operationStates.ToList()), new ReadOnlyCollection<TState>(successStates.ToList()));
        }

        /// <summary>
        /// Attempt to call <see cref="Event.TryFire"/>, returning false straight away if it failed,
        /// or a Task which completes when a transition from one of the <see cref="OperationStates"/> occurs
        /// otherwise.
        /// </summary>
        /// <remarks>It is NOT currently safe to call this from a transition handler</remarks>
        /// <param name="eventData">Event data to pass to <see cref="Event.TryFire"/></param>
        /// <param name="cancellationToken">Optional token which can cancel the operation</param>
        /// <returns>Task which completes if the event couldn't be fired, or when the operation completes</returns>
        public Task<bool> TryFireAsync(TEventData eventData, CancellationToken? cancellationToken = null)
        {
            return this.operationInner.InvokeAsync(() => this.Event.TryFire(eventData), cancellationToken ?? CancellationToken.None);
        }

        /// <summary>
        /// Attempt to call <see cref="Event.TryFire"/>, returning false straight away if it failed,
        /// or a Task which completes when a transition from one of the <see cref="OperationStates"/> occurs
        /// otherwise.
        /// </summary>
        /// <remarks>It is NOT currently safe to call this from a transition handler</remarks>
        /// <param name="eventData">Event data to pass to <see cref="Event.TryFire"/></param>
        /// <param name="timeout">Timeout after which to timeout the operation</param>
        /// <returns>Task which completes if the event couldn't be fired, or when the operation completes</returns>
        public Task<bool> TryFireAsync(TEventData eventData, TimeSpan timeout)
        {
            var cts = new CancellationTokenSource(timeout);
            return this.TryFireAsync(eventData, cts.Token);
        }

        /// <summary>
        /// Attempt to call <see cref="Event.Fire"/>, returning a Task containing the <see cref="TransitionFailedException"/>,
        /// or a Task which completes when a transition from one of the <see cref="OperationStates"/> occurs
        /// otherwise.
        /// </summary>
        /// <remarks>It is NOT currently safe to call this from a transition handler</remarks>
        /// <param name="eventData">Event data to pass to <see cref="Event.TryFire"/></param>
        /// <param name="cancellationToken">Optional token which can cancel the operation</param>
        /// <returns>Task which completes if the event couldn't be fired, or when the operation completes</returns>
        public Task FireAsync(TEventData eventData, CancellationToken? cancellationToken = null)
        {
            return this.operationInner.InvokeAsync(() => { this.Event.Fire(eventData); return true; }, cancellationToken ?? CancellationToken.None);
        }

        /// <summary>
        /// Attempt to call <see cref="Event.Fire"/>, returning a Task containing the <see cref="TransitionFailedException"/>,
        /// or a Task which completes when a transition from one of the <see cref="OperationStates"/> occurs
        /// otherwise.
        /// </summary>
        /// <remarks>It is NOT currently safe to call this from a transition handler</remarks>
        /// <param name="eventData">Event data to pass to <see cref="Event.TryFire"/></param>
        /// <param name="timeout">Timeout after which to timeout the operation</param>
        /// <returns>Task which completes if the event couldn't be fired, or when the operation completes</returns>
        public Task FireAsync(TEventData eventData, TimeSpan timeout)
        {
            var cts = new CancellationTokenSource(timeout);
            return this.FireAsync(eventData, cts.Token);
        }
    }
}
