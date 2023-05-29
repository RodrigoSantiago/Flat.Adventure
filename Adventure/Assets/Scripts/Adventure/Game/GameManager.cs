using System;
using System.Collections;
using UnityEngine;

namespace Adventure.Game {
    public class GameManager : MonoBehaviour {

        private static GameManager instance;
        
        private void Awake() {
            if (instance == null) {
                instance = this;
                DontDestroyOnLoad(gameObject);
            } else {
                Destroy(gameObject);
            }
        }

        public static Coroutine RunAsync(IEnumerator enumerator) {
            return instance.StartCoroutine(enumerator);
        }
    }
}