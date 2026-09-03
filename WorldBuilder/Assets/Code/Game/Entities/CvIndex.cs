using UnityEngine;

namespace Game.Entities {
    public class CvIndex : MonoBehaviour {
        private static long NextId = 0;

        public static void SetNextId(long prevMaxId) {
            NextId = prevMaxId;
        }
        
        public long Id { get; private set; }

        public CvIndex CvIndexSetup() {
            Id = ++NextId;
            return this;
        }
        
        public CvIndex CvIndexSetup(long id) {
            Id = id;
            return this;
        }
    }
}