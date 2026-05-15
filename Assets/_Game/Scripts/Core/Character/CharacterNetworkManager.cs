using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Ledsna
{
    public class CharacterNetworkManager : NetworkBehaviour
    {
        protected CharacterManager character;

        private Vector3 localNetworkPosition;
        private Quaternion localNetworkRotation = Quaternion.identity;
        private float localHorizontalMovement;
        private float localVerticalMovement;
        private float localMoveAmount;
        private bool localIsSprinting;
        private int localEndurance = 1;
        private float localCurrentStamina;
        private int localMaxStamina;

        protected bool UseLocalValues => character != null && character.sceneTestingMode && !IsSpawned;

        [Header("Position")]
        public NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>
            (Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>
            (Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);
        public Vector3 networkPositionVelocity;
        public float networkPositionSmoothTime = 0.1f;
        public float networkRotationSmoothTime = 0.1f;

        [Header("Animation")]
        public NetworkVariable<float> horizontalMovement = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> verticalMovement = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> moveAmount = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("Flags")]
        public NetworkVariable<bool> isSprinting = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("Stats")]
        public NetworkVariable<int> endurance = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<float> currentStamina = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        public NetworkVariable<int> maxStamina = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public Vector3 NetworkPositionValue
        {
            get => UseLocalValues ? localNetworkPosition : networkPosition.Value;
            set
            {
                if (UseLocalValues)
                    localNetworkPosition = value;
                else if (IsSpawned)
                    networkPosition.Value = value;
            }
        }

        public Quaternion NetworkRotationValue
        {
            get => UseLocalValues ? localNetworkRotation : networkRotation.Value;
            set
            {
                if (UseLocalValues)
                    localNetworkRotation = value;
                else if (IsSpawned)
                    networkRotation.Value = value;
            }
        }

        public float HorizontalMovementValue
        {
            get => UseLocalValues ? localHorizontalMovement : horizontalMovement.Value;
            set
            {
                if (UseLocalValues)
                    localHorizontalMovement = value;
                else if (IsSpawned)
                    horizontalMovement.Value = value;
            }
        }

        public float VerticalMovementValue
        {
            get => UseLocalValues ? localVerticalMovement : verticalMovement.Value;
            set
            {
                if (UseLocalValues)
                    localVerticalMovement = value;
                else if (IsSpawned)
                    verticalMovement.Value = value;
            }
        }

        public float MoveAmountValue
        {
            get => UseLocalValues ? localMoveAmount : moveAmount.Value;
            set
            {
                if (UseLocalValues)
                    localMoveAmount = value;
                else if (IsSpawned)
                    moveAmount.Value = value;
            }
        }

        public bool IsSprintingValue
        {
            get => UseLocalValues ? localIsSprinting : isSprinting.Value;
            set
            {
                if (UseLocalValues)
                    localIsSprinting = value;
                else if (IsSpawned)
                    isSprinting.Value = value;
            }
        }

        public int EnduranceValue
        {
            get => UseLocalValues ? localEndurance : endurance.Value;
            set
            {
                if (UseLocalValues)
                    localEndurance = value;
                else if (IsSpawned)
                    endurance.Value = value;
            }
        }

        public float CurrentStaminaValue
        {
            get => UseLocalValues ? localCurrentStamina : currentStamina.Value;
            set
            {
                if (UseLocalValues)
                    localCurrentStamina = value;
                else if (IsSpawned)
                    currentStamina.Value = value;
            }
        }

        public int MaxStaminaValue
        {
            get => UseLocalValues ? localMaxStamina : maxStamina.Value;
            set
            {
                if (UseLocalValues)
                    localMaxStamina = value;
                else if (IsSpawned)
                    maxStamina.Value = value;
            }
        }

        protected virtual void Awake()
        {
            character = GetComponent<CharacterManager>();
            localNetworkPosition = transform.position;
            localNetworkRotation = transform.rotation;
        }

        // A SERVER RPC
        [ServerRpc]
        public void NotifyTheServerOfActionAnimationServerRpc(ulong clientId, string animationId, bool applyRootMotion)
        {
            if (IsServer)
            {
                PlayActionAnimationForAllClientsClientRpc(clientId, animationId, applyRootMotion);
            }

        }

        [ClientRpc]
        public void PlayActionAnimationForAllClientsClientRpc(ulong clientId, string animationId, bool applyRootMotion)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                PerformActionAnimationFromServer(animationId, applyRootMotion);
            }
        }

        private void PerformActionAnimationFromServer(string animationId, bool applyRootMotion)
        {
            character.applyRootMotion = applyRootMotion;
            character.animator.CrossFade(animationId, 0.2f);
        }
    }
}
