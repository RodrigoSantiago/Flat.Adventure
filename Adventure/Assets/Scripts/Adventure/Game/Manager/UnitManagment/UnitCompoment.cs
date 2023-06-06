using System;
using Adventure.Game.Manager.ChunkManagment;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.UnitManagment {
    public class UnitCompoment : MonoBehaviour {

        private static Vector3[] forceSearch = {
            new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1),
            new Vector3(-1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 0, -1)
        };

        public float moveSpeed = 5.0f;
        public float rotateSpeed = 45.0f;
        public float jumpSpeed = 10.0f;
        public float radius = 0.5f;
        public Vector3 speed = new Vector3();
        public Vector3 dir = new Vector3(0, 0, 1);
        public Quaternion rot = Quaternion.identity;
        public float velocity = 0;

        [Header("Camera")] 
        public float cameraDistance = 5.0f;
        public float cameraAngle = 0.5f;
        public Transform cameraTransform;
        
        [Header("Ground")] 
        public float groundCheckDistance = 0.1f;
        public float groundExtraCheckDistance = 0.5f;
        public ChunkCollisionHit groundCollision;
        public ChunkCollisionHit groundCollisionExtra;
        public bool isGrounded { get => groundCollision; }
        public Vector3 groundNormal { get => groundCollision ? groundCollision.normal : groundCollisionExtra ? groundCollisionExtra.normal : Vector3.up; }

        [Header("Ceiling")] 
        public float ceilingCheckDistance = 0.1f;
        public ChunkCollisionHit ceilingCollision;
        public bool isCeiling { get => ceilingCollision; }
        
        [Header("Gravity")] 
        public float gravity = 0.9f;
        public float maxGravitySpeed = 10.0f;
        public float gravitySpeed;

        private float startJumpTime = 0;
        
        public Vector3 rotation;
        public GameObject debugPoint;
        public LineRenderer line;
        private bool locke;
        private Vector3 unitBackNormal;
        private Vector3 unitGroundNormal;

        public void Update() {
            var pos = transform.position;
            

            if (Input.GetKeyDown(KeyCode.Escape)) {
                locke = !locke;
            }
            if (locke) {
                cameraAngle += -Input.GetAxis("Mouse Y") / 100f;
                rotation.y += Input.GetAxis("Mouse X");
                Cursor.lockState = CursorLockMode.Locked;
                cameraAngle = Mathf.Clamp01(cameraAngle);
            } else {
                Cursor.lockState = CursorLockMode.None;
                if (Input.GetKey(KeyCode.A)) {
                    rotation.y -= rotateSpeed * Time.deltaTime;
                } else if (Input.GetKey(KeyCode.D)) {
                    rotation.y += rotateSpeed * Time.deltaTime;
                }
            }
            
            transform.rotation = Quaternion.Euler(rotation);

            Vector3 controlForce = new Vector3();
            if (Input.GetKey(KeyCode.W)) {
                if (groundCollision && transform.right != groundNormal) {
                    controlForce = Vector3.Cross(transform.right, groundNormal);
                } else {
                    controlForce = transform.forward;
                }
            } else if (Input.GetKey(KeyCode.S)) {
                if (groundCollision && transform.right != groundNormal) {
                    controlForce = -Vector3.Cross(transform.right, groundNormal);
                } else {
                    controlForce = -transform.forward;
                }
            }

            if (controlForce.y > 0.6) {
                controlForce = controlForce * (1 - controlForce.y) / (1 - 0.6f);
            }
           /* line.SetPosition(0, pos);
            line.SetPosition(1, pos + (groundCollision ? groundNormal * 2 : new Vector3(0, 0, 0)));
            line.SetPosition(2, pos);
            line.SetPosition(3, pos + controlForce * 2);
            line.SetPosition(4, pos);
            line.SetPosition(5, pos + Vector3.Cross(transform.right, groundNormal) * 2);*/

            speed = controlForce * (moveSpeed * Time.deltaTime) + new Vector3(0, gravitySpeed, 0);
            
            velocity = speed.magnitude;
            if (velocity != 0) {
                dir = Vector3.Normalize(speed);
                Vector3 rot1 = dir == Vector3.up ? Vector3.right : Vector3.Cross(dir, Vector3.up);
                Vector3 rot2 = dir == Vector3.up ? Vector3.left : -Vector3.Cross(dir, Vector3.up);
                Vector3 rot3 = rot1 == Vector3.up ? dir : Vector3.Cross(rot1, dir);
                Vector3 rot4 = rot1 == Vector3.up ? dir : -Vector3.Cross(rot1, dir);
                rot1 = Vector3.Slerp(rot1, dir, 0.5f);
                rot2 = Vector3.Slerp(rot2, dir, 0.5f);
                rot3 = Vector3.Slerp(rot3, dir, 0.5f);
                rot4 = Vector3.Slerp(rot4, dir, 0.5f);
                debugPoint.transform.position = pos + rot3 * radius * 2;
                line.positionCount = 7;
                line.SetPosition(0, pos);
                line.SetPosition(1, pos + rot1 * radius);
                line.SetPosition(2, pos + rot2 * radius);
                line.SetPosition(3, pos + rot3 * radius);
                line.SetPosition(4, pos + rot4 * radius);
                line.SetPosition(5, pos);
                line.SetPosition(6, pos + dir * (radius * 3));

                ChunkManager.physics.Raycast(pos + dir * radius, dir, velocity, out var dis);
                ChunkManager.physics.Raycast(pos + rot1 * radius, dir, velocity, out var dis1);
                ChunkManager.physics.Raycast(pos + rot2 * radius, dir, velocity, out var dis2);
                ChunkManager.physics.Raycast(pos + rot3 * radius, dir, velocity, out var dis3);
                ChunkManager.physics.Raycast(pos + rot4 * radius, dir, velocity, out var dis4);
                
                speed = dir * ((dis + dis1 + dis2 + dis3 + dis4) / 5f);
            }

            Vector3 outForce = GetOutForce() * (Time.deltaTime * 10);
            transform.position += speed + outForce;

            GroundCheck();
            CeilCheck();
            startJumpTime -= Time.deltaTime;
            if (isGrounded && startJumpTime <= 0) {
                gravitySpeed = 0;
            } else {
                gravitySpeed = Math.Max(gravitySpeed - gravity * Time.deltaTime, -maxGravitySpeed);
            }
            
            if (gravitySpeed > 0) {
                if (isCeiling) {
                    gravitySpeed = 0;
                }
            }
            
            if (Input.GetKeyDown(KeyCode.Space)) {
                if (isGrounded) {
                    gravitySpeed = jumpSpeed * Mathf.Clamp01(groundNormal.y);
                    startJumpTime = 0.5f;
                }
            }

            ChunkManager.current.position = transform.position;
            MoveCameraIdeal();
        }

        public void MoveCameraIdeal() {
            Vector3 unitBack;
            Vector3 unitUp;
            if (groundCollision) {
                unitUp = groundNormal;
                unitGroundNormal = Vector3.Slerp(unitGroundNormal, unitUp, Time.deltaTime * 5f);
            } else {
                unitUp = new Vector3(0, 1, 0);
                unitGroundNormal = Vector3.Slerp(unitGroundNormal, unitUp, Time.deltaTime);
            }
            unitBack = -Vector3.Cross(transform.right, unitGroundNormal);
            unitBackNormal = unitBack;//Vector3.Slerp(unitBackNormal, unitBack, Time.deltaTime * 5f);
            Vector3 lerpCamera = Vector3.Slerp(unitBackNormal, new Vector3(0, 1, 0), Mathf.Lerp(0.1f, 0.75f, Mathf.Clamp01(cameraAngle)));

            var pos = transform.position + lerpCamera * cameraDistance;

            cameraTransform.position = Vector3.Lerp(cameraTransform.position, pos, Time.deltaTime * 5f);
            cameraTransform.LookAt(transform.position + new Vector3(0, 1, 0));
        }

        public void GroundCheck() {
            var pos = transform.position;
            groundCollisionExtra = ChunkManager.physics.Raycast(pos, new Vector3(0, -1, 0), radius + groundExtraCheckDistance);
            if (groundCollision) {
                groundCollision = groundCollisionExtra;
            } else {
                groundCollision = ChunkManager.physics.Raycast(pos, new Vector3(0, -1, 0), radius + groundCheckDistance);
            }
        }

        public void CeilCheck() {
            var pos = transform.position;
            ceilingCollision = ChunkManager.physics.Raycast(pos, new Vector3(0, 1, 0), radius + ceilingCheckDistance);
        }
        
        public Vector3 GetOutForce() {
            var pos = transform.position;
            
            int count = 0;
            float totalOff = 0;
            Vector3 total = new Vector3();
            for (int i = 0; i < forceSearch.Length; i++) {
                if (ChunkManager.physics.Raycast(pos, forceSearch[i], radius, out var dis, out var col, out var nor)) {
                    float off = (radius - dis) / radius;
                    total += nor * off;
                    totalOff += off;
                    count++;
                }
            }

            if (count == 6 && totalOff > 5 && total.sqrMagnitude < 0.001) {
                return new Vector3(0, 1, 0);
            }
            return (count > 0 && totalOff > 0.01 ? total / count : new Vector3());
        }
    }
}