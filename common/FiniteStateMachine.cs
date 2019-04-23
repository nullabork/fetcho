using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fetcho.Common
{
    public enum DefaultTransitionPath
    {
        RootNode,
        Self
    }

    public enum FiniteStateMachineBooleanState
    {
        Nothing,
        Accept,
        Reject,
    }

    [Serializable]
    public class FiniteStateMachine<TStateType, TInputType> : IDisposable
    {
        private FiniteStateMachineNode<TStateType, TInputType> rootNode;

        public FiniteStateMachineNode<TStateType, TInputType> RootNode
        {
            get { return rootNode; }
        }

        /// <summary>
        /// A list of states and the number of references to them
        /// </summary>
        public Dictionary<TStateType, int> States { get; }

        public FiniteStateMachine()
        {
            rootNode = new FiniteStateMachineNode<TStateType, TInputType>();
            rootNode.DefaultTransition = rootNode;
            States = new Dictionary<TStateType, int>();
        }

        public FiniteStateMachineNode<TStateType, TInputType> Input(FiniteStateMachineNode<TStateType, TInputType> currentNode,
                                                                  IEnumerable<TInputType> inputs)
        {
            if (inputs == null) throw new ArgumentNullException("inputs");

            var startNode = currentNode;
            foreach (var input in inputs)
            {
                currentNode = Input(currentNode, input);
                if (currentNode == startNode)
                    return startNode;
            }
            return currentNode;
        }

        public FiniteStateMachineNode<TStateType, TInputType> Input(FiniteStateMachineNode<TStateType, TInputType> currentNode,
                                                                  TInputType input)
        {
            if (currentNode == null) throw new ArgumentNullException("currentNode");

            if (currentNode.Transitions.ContainsKey(input))
            {
                currentNode = currentNode.Transitions[input];
                //        Console.WriteLine("-> {0}", input);
            }
            else
            {
                currentNode = currentNode.DefaultTransition;
                //        Console.WriteLine("-> Default");
            }

            return currentNode;
        }

        public FiniteStateMachineNode<TStateType, TInputType> AddStateInput(FiniteStateMachineNode<TStateType, TInputType> currentNode,
                                                                           TStateType state,
                                                                           TInputType input,
                                                                           DefaultTransitionPath defaultTransition)
        {
            if (currentNode.Transitions.ContainsKey(input))
            {
                currentNode = currentNode.Transitions[input];
                if (!currentNode.State.Contains(state) &&
                    !EqualityComparer<TStateType>.Default.Equals(state, default(TStateType)))
                    currentNode.State.Add(state);

                if (defaultTransition == DefaultTransitionPath.Self)
                    currentNode.DefaultTransition = currentNode;
            }
            else
            {
                var node = new FiniteStateMachineNode<TStateType, TInputType>()
                {
                    DefaultTransition = rootNode,
                };

                if (!EqualityComparer<TStateType>.Default.Equals(state, default(TStateType)))
                    node.State.Add(state);

                if (defaultTransition == DefaultTransitionPath.Self)
                    node.DefaultTransition = node;

                currentNode.Transitions.Add(input, node);
                currentNode = node;
            }

            if (!EqualityComparer<TStateType>.Default.Equals(state, default(TStateType)))
            {
                // running total for the number of times this state is used
                if (!States.ContainsKey(state))
                    States.Add(state, 0);
                States[state]++;
            }
            return currentNode;
        }

        public void AddState(TStateType state, IEnumerable<TInputType> inputs)
        {
            if (inputs == null) throw new ArgumentNullException("inputs");

            FiniteStateMachineNode<TStateType, TInputType> node = rootNode;

            foreach (var input in inputs)
            {
                if (!node.Transitions.ContainsKey(input))
                {
                    node.Transitions.Add(input, new FiniteStateMachineNode<TStateType, TInputType>()
                    {
                        DefaultTransition = rootNode,
                    });
                }

                node = node.Transitions[input];
            }

            node.State.Add(state);
            if (!States.ContainsKey(state))
                States.Add(state, 0);
            States[state]++;
        }

        public void RemoveState(TStateType state, IEnumerable<TInputType> inputs)
        {
            if (inputs == null) throw new ArgumentNullException("inputs");

            FiniteStateMachineNode<TStateType, TInputType> node = rootNode;

            foreach (var input in inputs)
            {
                if (!node.Transitions.ContainsKey(input))
                    throw new ArgumentException("Unknown input: " + input);

                node = node.Transitions[input];
            }

            if (!node.State.Contains(state))
                throw new ArgumentException("State doesn't exist");

            node.State.Remove(state);
            States[state]--;
            if (States[state] <= 0)
                States.Remove(state);

        }

        public IEnumerable<TStateType> GetState(IEnumerable<TInputType> inputs)
        {
            var currentNode = rootNode;
            currentNode = Input(currentNode, inputs);
            return currentNode.State;
        }

        public void Clear()
        {
            if (rootNode != null)
                RecurseClear(rootNode);

            rootNode = null;
        }

        private void RecurseClear(FiniteStateMachineNode<TStateType, TInputType> node)
        {
            foreach (var state in node.State)
            {
                if (state is IDisposable d) d.Dispose();
            }

            node.State.Clear();
            node.DefaultTransition = null;

            foreach (var child in node.Transitions.Values)
                RecurseClear(child);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("FSM:");
            sb.Append(ToString(rootNode, 0));
            return sb.ToString();
        }

        private string ToString(FiniteStateMachineNode<TStateType, TInputType> node, int depth)
        {
            var sb = new StringBuilder();
            var csv = node.State.Aggregate("", (x, y) => x + ", " + y);
            if (csv.Length > 2) csv = csv.Substring(2);
            csv += " " + (node.DefaultTransition == rootNode ? "DEFAULT:RootNode" : "DEFAULT: Something else");
            sb.AppendLine(csv);
            foreach (var child in node.Transitions)
                sb.Append(node.Transitions.Count.ToString().PadRight(depth) +
                    child.Key +
                    " -> " +
                    ToString(child.Value, depth + 1));
            return sb.ToString();
        }

        protected virtual void Dispose(bool disposable)
        {
            Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
