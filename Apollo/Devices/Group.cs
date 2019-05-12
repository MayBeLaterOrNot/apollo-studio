using System;
using System.Collections.Generic;
using System.Linq;

using Apollo.Core;
using Apollo.Elements;
using Apollo.Structures;

namespace Apollo.Devices {
    public class Group: Device, IMultipleChainParent, ISelectParent {
        public static readonly new string DeviceIdentifier = "group";

        public IMultipleChainParentViewer SpecificViewer {
            get => (IMultipleChainParentViewer)Viewer.SpecificViewer;
        }

        public ISelectParentViewer IViewer {
            get => (ISelectParentViewer)Viewer.SpecificViewer;
        }

        public List<ISelect> IChildren {
            get => Chains.Select(i => (ISelect)i).ToList();
        }

        public bool IRoot {
            get => false;
        }

        private Action<Signal> _midiexit;
        public override Action<Signal> MIDIExit {
            get => _midiexit;
            set {
                _midiexit = value;
                Reroute();
            }
        }

        public List<Chain> Chains = new List<Chain>();

        private void Reroute() {
            for (int i = 0; i < Chains.Count; i++) {
                Chains[i].Parent = this;
                Chains[i].ParentIndex = i;
                Chains[i].MIDIExit = ChainExit;
            }
        }

        public Chain this[int index] {
            get => Chains[index];
        }

        public int Count {
            get => Chains.Count;
        }

        public override Device Clone() => new Group((from i in Chains select i.Clone()).ToList(), Expanded);

        public void Insert(int index, Chain chain = null) {
            Chains.Insert(index, chain?? new Chain());
            Reroute();
        }

        public void Add(Chain chain) {
            Chains.Add(chain);
            Reroute();
        }

        public void Remove(int index, bool dispose = true) {
            if (dispose) Chains[index].Dispose();
            Chains.RemoveAt(index);
            Reroute();
        }

        public int? Expanded;

        public Group(List<Chain> init = null, int? expanded = null): base(DeviceIdentifier) {
            foreach (Chain chain in init?? new List<Chain>()) Chains.Add(chain);
            Expanded = expanded;

            Reroute();
        }

        private void ChainExit(Signal n) => MIDIExit?.Invoke(n);

        public override void MIDIEnter(Signal n) {
            if (Chains.Count == 0) ChainExit(n);

            foreach (Chain chain in Chains)
                chain.MIDIEnter(n.Clone());
        }

        public override void Dispose() {
            foreach (Chain chain in Chains) chain.Dispose();
            base.Dispose();
        }
    }
}