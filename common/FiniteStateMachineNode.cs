using System;
using System.Collections.Generic;


namespace Fetcho.Common
{
    [Serializable]
    public class FiniteStateMachineNode<TStateType, TInputType>
    {
        public List<TStateType> State { get; }

        public Dictionary<TInputType, FiniteStateMachineNode<TStateType, TInputType>> Transitions { get; }
        public FiniteStateMachineNode<TStateType, TInputType> DefaultTransition { get; set; }

        public FiniteStateMachineNode()
        {
            State = new List<TStateType>();
            Transitions = new Dictionary<TInputType, FiniteStateMachineNode<TStateType, TInputType>>();
        }

    }
}
