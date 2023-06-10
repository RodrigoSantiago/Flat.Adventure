using System;
using Adventure.Game.Manager.ChunkManagment;
using Adventure.Game.Manager.UnitManagment.Controllers;
using Adventure.Game.Manager.UnitManagment.Weapons;
using Adventure.Logic.Data;
using UnityEngine;

namespace Adventure.Game.Manager.UnitManagment {
    public class UnitCompoment : MonoBehaviour {
        public enum VerticalState { STAND, JUMP, FALL };
        
        private static Vector3[] forceSearch = {
            new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1),
            new Vector3(-1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 0, -1)
        };
        
        private readonly Vector3[] fourPoints = {
            Vector3.Slerp(Vector3.forward, Vector3.up, 0.5f),
            Vector3.Slerp(Vector3.forward, Vector3.down, 0.5f),
            Vector3.Slerp(Vector3.forward, Vector3.left, 0.5f),
            Vector3.Slerp(Vector3.forward, Vector3.right, 0.5f),
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
        
        [Header("Movement")] 
        public VerticalState verState = VerticalState.FALL;
        public float inputForce;
        public Vector3 moveDir;
        
        [Header("Ground")] 
        public float groundDistNormal = 0.5f;
        public float groundDistToFall = 0.2f;
        public float groundDistToStand = 0.1f;
        public float ceilingDist = 0.1f;
        
        public ChunkCollisionHit upCollision;
        public ChunkCollisionHit downCollision;
        public ChunkCollisionHit fowardCollision;
        
        public Vector3 groundNormal { get => downCollision ? downCollision.normal : Vector3.up; }

        [Header("Gravity")] 
        public float gravity = 0.9f;
        public float maxGravitySpeed = 2.0f;
        public float gravitySpeed;

        private float jumpTimer = 0;
        
        public Vector3 rotation;
        public GameObject debugPoint;
        public LineRenderer line;

        private bool locke = true;
        
        private Vector3 unitBackNormal;
        private Vector3 unitGroundNormal;
        private bool lastJump;
        private float aimTime;

        public Animator anim;
        public UnitController controller;
        public Weapon leftWeapon;
        public Weapon rightWeapon;

        private void Start() {
            
        }

        public void OnFootstep() {
            
        }

        public void Update() {
            var pos = transform.position;
            
            CalculateInputValues();

            GroundCheck();
            
            StateChange();
            StateLoop();

            line.positionCount = 3;
            line.SetPosition(0, pos);
            line.SetPosition(1, pos + moveDir * 2);
            line.SetPosition(2, pos - moveDir * 2);

            if (downCollision && Input.GetKeyDown(KeyCode.Space)) {
                gravitySpeed = jumpSpeed;
                jumpTimer = 0.5f;
                verState = VerticalState.JUMP;
                anim.SetBool("Jump", true);
            } else {
                anim.SetBool("Jump", false);
            }

            if (Input.GetKeyDown(KeyCode.Q)) {
                anim.SetTrigger("Attack");
            }

            speed = (moveDir * (moveSpeed * Time.deltaTime)) + (new Vector3(0, gravitySpeed, 0) * Time.deltaTime);
            velocity = speed.magnitude;
            if (velocity > 0.001) {
                dir = speed / velocity;
            }
            
            anim.SetFloat("Speed", moveDir.magnitude * moveSpeed);
            anim.SetBool("FreeFall", downCollision.distance >= radius + groundDistToFall);
            anim.SetBool("Grounded", downCollision.distance < radius + groundDistToFall);
            
            Vector3 outForce = GetOutForce() * (Time.deltaTime * 10);
            transform.position += CastMoveVector(dir, velocity) + outForce;
            transform.rotation = Quaternion.Euler(rotation);

            ChunkManager.current.position = transform.position;
            MoveCameraIdeal();
        }

        private void StateChange() {
            if (verState == VerticalState.FALL) {
                if (downCollision && downCollision.distance < radius + groundDistToStand) {
                    verState = VerticalState.STAND;
                }
            } else if (verState == VerticalState.STAND) {
                if (!downCollision || downCollision.distance >= radius + groundDistToFall) {
                    verState = VerticalState.FALL;
                }
            } else if (verState == VerticalState.JUMP) {
                jumpTimer -= Time.deltaTime;
                CeilCheck();
                if (jumpTimer <= 0 || upCollision) {
                    if (downCollision && downCollision.distance < radius + groundDistToStand) {
                        verState = VerticalState.STAND;
                    } else {
                        verState = VerticalState.FALL;
                    }
                }
            }
        }

        private void CalculateInputValues() {
            if (Input.GetKeyDown(KeyCode.Escape)) {
                locke = !locke;
            }
            if (locke) {
                if (Input.GetMouseButton(0) || Input.GetMouseButton(1)) {
                    aimTime = 0.5f;
                } else if ((aimTime -= Time.deltaTime) <= 0) {
                    cameraAngle += -Input.GetAxis("Mouse Y") / 100f;
                    cameraAngle = Mathf.Clamp01(cameraAngle);
                }
            } else {
            }

            rotation.y = controller.rotation;

            float acc = (moveSpeedAcc > 0 ? 1 / moveSpeedAcc : 999) * Time.deltaTime;
            float inputVer = controller.axis.y;
            inputForce = inputForce < inputVer
                ? (inputForce + acc < inputVer ? inputForce + acc : inputVer)
                : (inputForce - acc > inputVer ? inputForce - acc : inputVer); 
            
            if (downCollision) {
                moveDir = Vector3.Cross(transform.right, downCollision.normal) * inputForce;
            } else {
                moveDir = transform.forward * inputForce;
            }
            
            if (downCollision && moveDir.y > 0) {
                moveDir.y *= ReverseLerp(0.1f, 0.7f, downCollision.normal.y);
            }
        }

        private void StateLoop() {
            if (verState == VerticalState.FALL) {
                gravitySpeed = Mathf.Max(-maxGravitySpeed, gravitySpeed - gravity * Time.deltaTime);
                
            } else if (verState == VerticalState.STAND) {
                if (downCollision.distance >= radius + 0.05f && downCollision.normal.y > 0.8) {
                    gravitySpeed = Mathf.Max(-maxGravitySpeed, gravitySpeed - gravity * Time.deltaTime);
                } else {
                    gravitySpeed = 0;
                }
                
            } else if (verState == VerticalState.JUMP) {
                gravitySpeed = Mathf.Max(-maxGravitySpeed, gravitySpeed - gravity * Time.deltaTime);
                
            } 
        }

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
            unitGroundNormal = Vector3.Slerp(unitGroundNormal, groundNormal, Time.deltaTime * (downCollision ? 5f : 1f));
            unitBackNormal = -Vector3.Cross(transform.right, unitGroundNormal);
            float angle = 1 - Mathf.Clamp01(Vector3.Angle(unitBackNormal, new Vector3(0, 1, 0)) / 90);
            Vector3 lerpCamera = Vector3.Slerp(unitBackNormal, new Vector3(0, 1, 0), Mathf.Lerp(0.05f, 0.75f, cameraAngle));

            float distance = 1 + (cameraDistance * Mathf.Lerp(1f, 0f, angle * (1 - cameraAngle)));
            var pos = transform.position + lerpCamera * distance;

            cameraTransform.position = pos;//Vector3.Lerp(cameraTransform.position, pos, Time.deltaTime * 5f);
            cameraTransform.LookAt(transform.position + new Vector3(0, 1, 0));
        }

        private void GroundCheck() {
            var pos = transform.position;
            downCollision = ChunkManager.physics.Raycast(pos, new Vector3(0, -1, 0), radius + groundDistNormal);
        }

        private void CeilCheck() {
            var pos = transform.position;
            upCollision = ChunkManager.physics.Raycast(pos, new Vector3(0, 1, 0), radius + ceilingDist);
        }
        
        private static float ReverseLerp(float min, float max, float value) {
            return Mathf.Clamp01((value - min) / (max - min));
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