using UnityEngine;

namespace Adventure.Logic {
    public class Unit {
        public static int idProvider = 0;

        public readonly int id;
        
        public World world;
        public Vector3 position;

        public float hpMax;
        public float hp;

        public Vector3Int Local {
            get {
                return new Vector3Int(
                    Mathf.RoundToInt(position.x/16) * 16, 
                    Mathf.RoundToInt(position.y/16) * 16,
                    Mathf.RoundToInt(position.z/16) * 16);
            }
        }

        public Unit(World world, Vector3 position) {
            this.id = ++idProvider;
            this.world = world;
            this.position = position;
        }
    }
}