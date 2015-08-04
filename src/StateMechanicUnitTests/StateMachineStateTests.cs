﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using StateMechanic;

namespace StateMechanicUnitTests
{
    [TestFixture]
    public class StateMachineStateTests
    {
        [Test]
        public void StateMachineStartsInInitialState()
        {
            var sm = new StateMachine("state machine");
            var initialState = sm.CreateInitialState("initial state");
            var state1 = sm.CreateState("state1");

            Assert.AreEqual(initialState, sm.CurrentState);
            Assert.AreEqual(initialState, sm.InitialState);
        }

        [Test]
        public void CurrentStateReflectsCurrentState()
        {
            var sm = new StateMachine("state machine");
            var initialState = sm.CreateInitialState("initial state");
            var state1 = sm.CreateState("state1");
            var evt = sm.CreateEvent("evt");
            initialState.TransitionOn(evt).To(state1);

            Assert.AreEqual(initialState, sm.CurrentState);

            evt.Fire();

            Assert.AreEqual(state1, sm.CurrentState);
        }

        [Test]
        public void CurrentStateRecursiveReflectsCurrentStateOfChild()
        {
            var sm = new StateMachine("state machine");
            var initialState = sm.CreateInitialState("initial state");
            var state1 = sm.CreateState("state1");
            var childSm = state1.CreateChildStateMachine("childsm");
            var childInitialState = childSm.CreateInitialState("childInitialState");
            var childState1 = childSm.CreateState("childState1");

            var evt = sm.CreateEvent("evt");
            var evt2 = sm.CreateEvent("evt2");

            initialState.TransitionOn(evt).To(state1);
            childInitialState.TransitionOn(evt).To(childState1);
            state1.TransitionOn(evt2).To(initialState);

            Assert.AreEqual(initialState, sm.CurrentStateRecursive);

            evt.Fire();

            Assert.AreEqual(childInitialState, sm.CurrentStateRecursive);

            evt.Fire();

            Assert.AreEqual(childState1, sm.CurrentStateRecursive);

            evt2.Fire();

            Assert.AreEqual(initialState, sm.CurrentStateRecursive);
        }

        [Test]
        public void ChildStateMachineStartsInInitialStateIfParentStateIsInitialState()
        {
            var parent = new StateMachine("parent");
            var initial = parent.CreateInitialState("initial");
            var child = initial.CreateChildStateMachine("child");
            var childInitial = child.CreateInitialState("childInitial");

            Assert.AreEqual(childInitial, child.CurrentState);
            Assert.AreEqual(childInitial, child.InitialState);
        }

        [Test]
        public void ChildStateMachineStartsInNoStateIfParentStateIsNotInitialState()
        {
            var parent = new StateMachine("parent");
            var initial = parent.CreateInitialState("initial");
            var state1 = parent.CreateState("state1");
            var child = state1.CreateChildStateMachine("child");
            var childInitial = child.CreateInitialState("childInitial");

            Assert.AreEqual(null, child.CurrentState);
            Assert.AreEqual(childInitial, child.InitialState);
        }
    }
}
