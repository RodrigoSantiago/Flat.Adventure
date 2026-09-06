using System;
using UnityEngine;

namespace Game.Entities.Player {
    public class CvPlayerUnit : MonoBehaviour {

        public CvPlayerUnit Setup() {
            return this;
        }
        
        private void Update() {
            GameManager.Instance.PlayerSetView(this);
            
            var cam = GameManager.Instance.PlayerCamera;
            cam.transform.position = transform.position + new Vector3(-16, 16, -16);
            cam.transform.LookAt(transform);
        }
    }
}