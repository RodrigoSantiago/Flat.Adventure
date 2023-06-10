using System;
using UnityEngine;

#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace Adventure.Game.Manager.UnitManagment.Controllers {
    public class UnitControllerMouse : UnitController {

        public override Vector2 axis {
            get {
                float vertical = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
                float horizontal = (Input.GetKey(KeyCode.A) ? 1 : 0) - (Input.GetKey(KeyCode.D) ? 1 : 0);
                return new Vector2(horizontal, vertical);
            }
        }

        private float mouseRotation;
        public override float rotation {
            get {
                return mouseRotation;
            }
        }

        private bool primaryMousePress;
        private bool primaryMouseHolding;
        private float primaryMousePressTimer;
        private Vector3 primaryMousePos;

        private void Update() {
            if (!primaryMousePress) {
                if (Input.GetMouseButtonDown(0)) {
                    primaryMousePress = true;
                    primaryMousePressTimer = 0f;
                    primaryMousePos = Input.mousePosition;
                    
                    Cursor.lockState = CursorLockMode.None;
                    GetCursorPos(out var cursorPos);
                    if (Input.GetMouseButton(0)) {
                        SetCursorPos(cursorPos.x + Screen.width / 8, cursorPos.y);
                    }
                }
            }
            if (primaryMousePress) {
                if (primaryMousePressTimer > 0.5f) {
                    primaryMouseHolding = true;
                    unit.rightWeapon.OnHoldStart(HandSide.PRIMARY);
                }
                primaryMousePressTimer += Time.deltaTime;
                
                if (!Input.GetMouseButton(0)) {
                    if (primaryMouseHolding) {
                        unit.rightWeapon.OnHoldEnd(HandSide.PRIMARY);
                    } else {
                        unit.rightWeapon.RequestTrust(HandSide.PRIMARY);
                    }

                    primaryMousePress = false;
                    primaryMouseHolding = false;
                    primaryMousePressTimer = 0f;
                }
            }
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) {
                Cursor.lockState = CursorLockMode.Locked;
                mouseRotation += Input.GetAxis("Mouse X");
            }
        }
        
#if UNITY_STANDALONE_WIN
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Vector2Int point);
#else
        static bool SetCursorPos(int x, int y) {
            return true;
        }
        static bool GetCursorPos(out Vector2Int point) {
            point = new Vector2Int(0, 0);
            return true;
        }
#endif
        
    }
}