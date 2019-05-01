using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace Apollo.Structures {
    public class Offset {
        public static readonly string Identifier = "offset";

        private int _x = 0;
        public int X {
            get => _x;
            set {
                if (-9 <= value && value <= 9)
                    _x = value;
            }
        }

        private int _y = 0;
        public int Y {
            get => _y;
            set {
                if (-9 <= value && value <= 9)
                    _y = value;
            }
        }

        public Offset Clone() => new Offset(X, Y);

        public Offset(int x = 0, int y = 0) {
            X = x;
            Y = y;
        }
    }
}