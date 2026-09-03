using Game.Data;
using Game.Worlds;
using UnityEngine;

namespace Game.GameTest {
    public class TestViewCenter : MonoBehaviour {
        public GameObject cube;
        public GameObject cube0;
        public GameObject cube1;
        public GameObject cube2;

        private void Update() {
            var pos = IndexPos.FromVector3(transform.position);
            var manager = GameManager.Instance.OverWorld;
            if (cube) cube.transform.position = manager.view.ToVector3();
            if (cube0) cube0.transform.position = manager.viewLod0.ToVector3();
            if (cube1) cube1.transform.position = manager.viewLod1.ToVector3();
            if (cube2) cube2.transform.position = manager.viewLod2.ToVector3();
        }
    }
}