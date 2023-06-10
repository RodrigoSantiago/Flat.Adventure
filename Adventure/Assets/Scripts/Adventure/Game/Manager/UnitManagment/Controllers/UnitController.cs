using UnityEngine;

namespace Adventure.Game.Manager.UnitManagment.Controllers {
    
    public class UnitController : MonoBehaviour {
        
        public UnitCompoment unit { get; protected set; }

        protected virtual void Start() {
            this.unit = GetComponent<UnitCompoment>();
        }
        
        public virtual Vector2 axis { get; }
        
        public virtual float rotation { get; }
    }
}