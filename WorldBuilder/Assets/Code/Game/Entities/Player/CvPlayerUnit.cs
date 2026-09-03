using System;
using UnityEngine;

namespace Game.Entities.Player {
    public class CvPlayerUnit : MonoBehaviour {

        public CvPlayerUnit Setup() {
            return this;
        }
        
        private void Update() {
            GameManager.Instance.PlayerSetView(this);
        }
    }
}