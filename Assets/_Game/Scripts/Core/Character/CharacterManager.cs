using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ledsna
{
    public class CharacterManager : NetworkBehaviour
    {
        [HideInInspector] public CharacterController characterController;
        [HideInInspector] public Animator animator;
        [HideInInspector] public CharacterNetworkManager characterNetworkManager;

        [Header("SCENE TESTING")]
        [Tooltip("Enable this to test character in scene without networking. Bypasses all IsOwner checks.")]
        public bool sceneTestingMode = false;

        [Header("FLAGS")]
        public bool isPerformingAction = false;
        public bool isJumping = false;
        public bool isGrounded = true;
        public bool applyRootMotion = false;
        public bool canRotate = true;
        public bool canMove = true;

        protected virtual void Awake()
        {
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);

            animator = GetComponent<Animator>();
            characterController = GetComponent<CharacterController>();
            characterNetworkManager = GetComponent<CharacterNetworkManager>();
        }

        protected virtual void Update()
        {
            animator.SetBool("IsGrounded", isGrounded);

            // In scene testing mode, always act as owner
            bool isLocalOwner = sceneTestingMode || IsOwner;

            if (isLocalOwner)
            {
                if (characterNetworkManager != null)
                {
                    characterNetworkManager.NetworkPositionValue = transform.position;
                    characterNetworkManager.NetworkRotationValue = transform.rotation;
                }
            }
            else
            {
                transform.position = Vector3.SmoothDamp
                    (transform.position,
                    characterNetworkManager.NetworkPositionValue,
                    ref characterNetworkManager.networkPositionVelocity,
                    characterNetworkManager.networkPositionSmoothTime);
                transform.rotation = Quaternion.Slerp
                    (transform.rotation,
                    characterNetworkManager.NetworkRotationValue,
                    characterNetworkManager.networkRotationSmoothTime);
            }
        }

        protected virtual void LateUpdate()
        {
        }
    }
}
