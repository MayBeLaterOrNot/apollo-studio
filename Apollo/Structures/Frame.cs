using System.Collections.Generic;
using System.Linq;

using Apollo.Components;
using Apollo.Devices;
using Apollo.Selection;
using Apollo.Undo;

namespace Apollo.Structures {
    public class Frame: ISelect {
        public ISelectViewer IInfo {
            get => Info;
        }

        public ISelectParent IParent {
            get => Parent;
        }

        public int? IParentIndex {
            get => ParentIndex;
        }
        
        public ISelect IClone() => (ISelect)Clone();
        
        Color[] _screen;
        public Color[] Screen {
            get => _screen;
            set {
                if (_screen == null || !_screen.SequenceEqual(value)) {
                    _screen = value;

                    Info?.Viewer?.Draw();
                    
                    Parent?.Window?.SetGrid(ParentIndex.Value, this);
                }
            }
        }

        public FrameDisplay Info;
        public Pattern Parent;
        public int? ParentIndex;

        Time _time;
        public Time Time {
            get => _time;
            set {
                if (_time != null) {
                    _time.FreeChanged -= FreeChanged;
                    _time.ModeChanged -= ModeChanged;
                    _time.StepChanged -= StepChanged;
                }

                _time = value;

                if (_time != null) {
                    _time.Minimum = 10;
                    _time.Maximum = 30000;

                    _time.FreeChanged += FreeChanged;
                    _time.ModeChanged += ModeChanged;
                    _time.StepChanged += StepChanged;

                    FreeChanged(_time.Free);
                    ModeChanged(_time.Mode);
                    StepChanged(_time.Length);
                }
            }
        }

        void FreeChanged(int value) {
            Parent?.Window?.SetDurationValue(ParentIndex.Value, Time.Free);
            if (Info != null) Info.Viewer.Time.Text = ToString();
        }

        void ModeChanged(bool value) {
            Parent?.Window?.SetDurationMode(ParentIndex.Value, Time.Mode);
            if (Info != null) Info.Viewer.Time.Text = ToString();
        }

        void StepChanged(Length value) {
            Parent?.Window?.SetDurationStep(ParentIndex.Value, Time.Length);
            if (Info != null) Info.Viewer.Time.Text = ToString();
        }

        public Frame Clone() => new Frame(Time.Clone(), (from i in Screen select i.Clone()).ToArray());

        public Frame(Time time = null, Color[] screen = null) {
            if (screen == null || screen.Length != 101) {
                screen = new Color[101];
                for (int i = 0; i < 101; i++) screen[i] = new Color(0);
            }

            Time = time?? new Time();
            Screen = screen;
        }

        public void Invert() => Screen = Screen.SkipLast(1).Reverse().Concat(Screen.TakeLast(1)).ToArray();

        public override string ToString() => (Parent.Infinite && ParentIndex.Value == Parent.Count - 1)? "Infinite" : Time.ToString();

        public void Dispose() {
            Time.Dispose();
            Info = null;
            Parent = null;
            ParentIndex = null;
        }

        public class DragDropUndoEntry: PathUndoEntry<Pattern> {
            bool copy;
            int count, before, before_pos, after, after_pos;

            protected override void UndoPath(params Pattern[] item) {
                if (copy)
                    for (int i = after + count; i > after; i--)
                        item[1].Remove(i);

                else Frame.Move(
                    (from i in Enumerable.Range(after_pos + 1, count) select item[1][i]).ToList(),
                    item[0],
                    before_pos
                );
            }

            protected override void RedoPath(params Pattern[] item) => Frame.Move((
                from i in Enumerable.Range(before + 1, count) select item[0][i]).ToList(),
                item[1],
                after,
                copy
            );
            
            public DragDropUndoEntry(Pattern sourcepattern, Pattern targetpattern, bool copy, int count, int before, int after, int before_pos, int after_pos)
            : base($"Pattern Frame {(copy? "Copied" : "Moved")}", sourcepattern, targetpattern) {
                this.copy = copy;
                this.count = count;
                this.before = before;
                this.after = after;
                this.before_pos = before_pos;
                this.after_pos = after_pos;
            }
        }
    }
}