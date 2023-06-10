using UnityEngine;

namespace Adventure.Game.Manager.UnitManagment.Weapons {
    public class WeaponSword : Weapon {
        public override void RequestTrust(HandSide side) {
            if (unit.anim.GetCurrentAnimatorStateInfo(1).IsName("Idle")) {
                unit.anim.SetTrigger("Attack");
            }
        }
    }
}