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
        public float moveSpeedAcc = 5.0f;
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
        private bool locke = true;
        private Vector3 unitBackNormal;
        private Vector3 unitGroundNormal;
        private float acc;
        private bool lastJump;

        public Animator anim;

        private void Awake() {
            anim.SetFloat("MotionSpeed", 1);
        }

        public void OnFootstep() {
            
        }

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

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S)) {
                acc += moveSpeedAcc * Time.deltaTime;
            } else {
                acc -= moveSpeedAcc * Time.deltaTime;
            }

            acc = Mathf.Clamp01(acc);

            if (controlForce.y > 0.5) {
                controlForce *= Mathf.Clamp01(1 - ((controlForce.y - 0.5f) * 4f));
            }

            anim.SetFloat("Speed", acc * moveSpeed);

            speed = controlForce * (moveSpeed * acc * Time.deltaTime) + new Vector3(0, gravitySpeed, 0);
            
            velocity = speed.magnitude;
            if (velocity != 0) {
                dir = speed / velocity;
                speed = CastMoveVector(dir, velocity);
            }

            Vector3 outForce = GetOutForce() * (Time.deltaTime * 10);
            transform.position += speed + outForce;

            GroundCheck();
            CeilCheck();
            startJumpTime -= Time.deltaTime;
            if (isGrounded && startJumpTime <= 0) {
                gravitySpeed = 0;
                anim.SetBool("Grounded", true);
                anim.SetBool("FreeFall", false);
                if (lastJump) {
                    anim.SetBool("Jump", false);
                    lastJump = false;
                }
            } else {
                gravitySpeed = Math.Max(gravitySpeed - gravity * Time.deltaTime, -maxGravitySpeed);
                if (!lastJump) {
                    anim.SetBool("Grounded", false);
                    anim.SetBool("FreeFall", true);
                    lastJump = true;
                }
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
                    anim.SetBool("Jump", true);
                    lastJump = true;
                }
            }

            ChunkManager.current.position = transform.position;
            MoveCameraIdeal();
        }

        private readonly Vector3[] fourPoints = {
            Vector3.Slerp(Vector3.forward, Vector3.up, 0.5f),
            Vector3.Slerp(Vector3.forward, Vector3.down, 0.5f),
            Vector3.Slerp(Vector3.forward, Vector3.left, 0.5f),
            Vector3.Slerp(Vector3.forward, Vector3.right, 0.5f),
        };

        public Vector3 CastMoveVector(Vector3 dir, float speed) {
            var pos = transform.position;
            
            Quaternion fromRot = Quaternion.FromToRotation(Vector3.forward, dir);
            Vector3 rot1 = fromRot * fourPoints[0];
            Vector3 rot2 = fromRot * fourPoints[1];
            Vector3 rot3 = fromRot * fourPoints[2];
            Vector3 rot4 = fromRot * fourPoints[3];

            ChunkManager.physics.Raycast(pos + dir * radius, dir, speed, out var dis);
            ChunkManager.physics.Raycast(pos + rot1 * radius, dir, speed, out var dis1);
            ChunkManager.physics.Raycast(pos + rot2 * radius, dir, speed, out var dis2);
            ChunkManager.physics.Raycast(pos + rot3 * radius, dir, speed, out var dis3);
            ChunkManager.physics.Raycast(pos + rot4 * radius, dir, speed, out var dis4);
            
            return dir * ((dis + dis1 + dis2 + dis3 + dis4) / 5f);
        }
        
        public void MoveCameraIdeal() {
            unitGroundNormal = Vector3.Slerp(unitGroundNormal, groundNormal, Time.deltaTime * (groundCollision ? 5f : 1f));
            unitBackNormal = -Vector3.Cross(transform.right, unitGroundNormal);
            float angle = 1 - Mathf.Clamp01(Vector3.Angle(unitBackNormal, new Vector3(0, 1, 0)) / 90);
            Vector3 lerpCamera = Vector3.Slerp(unitBackNormal, new Vector3(0, 1, 0), Mathf.Lerp(0.05f, 0.75f, cameraAngle));

            float distance = 1 + (cameraDistance * Mathf.Lerp(1f, 0f, angle * (1 - cameraAngle)));
            var pos = transform.position + lerpCamera * distance;

            cameraTransform.position = pos;//Vector3.Lerp(cameraTransform.position, pos, Time.deltaTime * 5f);
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

            groundCollision.hit &= groundNormal.y > 0.6f;
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