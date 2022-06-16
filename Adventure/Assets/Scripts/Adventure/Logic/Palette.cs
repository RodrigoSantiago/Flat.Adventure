using System;

namespace Adventure.Logic {
    public class Palette {
        private short[] list;
        private int used;
        
        public int Size { get { return used; } }
        
        public int Bits { get; private set; }
        
        public bool IsCommon { get; private set; }
        
        public long Signature { get; private set; }

        public Palette(bool common, short[] readTypes, int usedTypes, short extraType = 0) {
            IsCommon = common;
            Bits = 1;

            int add = (extraType == 0 ? 0 : 1);
            int values = 256;
            for (int i = 4; i <= 256; i += i) {
                Bits++;
                if (usedTypes + add < i) {
                    values = i;
                    break;
                }
            }

            used = usedTypes + add;
            list = new short[values];
            for (int i = 0; i < usedTypes + add; i++) {
                short type = i == usedTypes ? extraType : readTypes[i];
                list[i] = type;
                Signature |= 1L << (type % 64);
            }
        }

        public bool Contains(short type) {
            long bit = 1L << (type % 64);

            if ((Signature & bit) == bit) {
                for (int i = 0; i < list.Length; i++) {
                    if (list[i] == type) {
                        return true;
                    }
                }
            }

            return false;
        }

        public int Read(int index) {
            return index < used ? list[index] : 0;
        }

        public Palette Add(short type) {
            if (Contains(type)) {
                return this;
            }
            
            if (used + 1 >= list.Length && list.Length == 256) {
                return null;
            }
            
            if (IsCommon) {
                return new Palette(false, list, used, type);
            }
                
            if (used + 1 >= list.Length) {
                Bits++;
                Array.Resize(ref list, list.Length * 2);
            }
            
            list[used++] = type;
            Signature |= 1L << (type % 64);
            
            return this;
        }
    }
}