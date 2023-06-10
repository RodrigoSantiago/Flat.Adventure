using Adventure.Game.Manager.UnitManagment.Controllers;
using UnityEngine;

namespace Adventure.Game.Manager.UnitManagment.Weapons {
    public class Weapon : MonoBehaviour {

        public UnitCompoment unit;

        public virtual void RequestSwingLine(HandSide side, float angle, float distance) {
            // Call an animation of an attack > the animation call OnSwing
        }
        
        public virtual void RequestTrust(HandSide side) {
            // Call an animation of an attack > the animation call OnSwing, specialized foward swing
        }

        public virtual void RequestRenderRune(Vector3 p1, Vector3 p2, float duration) {
            // Call a render to draw a Rune Line
        }

        /**
         * Called on receiving powerfull damage, effects or something that can break rune writing, movements and combos
         */
        public virtual void Interrupt() {
            // Prevent some special attacks to happen
        }
        
        /**
         * Special Button Pressed/Release - Set
         */
        public virtual void OnSpecialSet(HandSide side, bool active) {
            
        }
        
        /**
         * Special Button Pressed/Release - Toggle
         */
        public virtual void OnSpecialToggle(HandSide side) {
            
        }

        /**
         * After some miliseconds Pressing
         */
        public virtual void OnHoldStart(HandSide side) {
            
        }

        /**
         * After releasing
         */
        public virtual void OnHoldEnd(HandSide side) {
            
        }
        
        public virtual void OnSwing(HandSide side, Vector3 direction, Vector3 position, Vector3 prevPosition) {
            
        }

        public virtual void OnRune(RuneShape shape, float precision) {
            
        }
    }
}