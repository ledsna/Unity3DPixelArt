using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ledsna
{
    public class PlayerManager : CharacterManager
    {
        [HideInInspector] public PlayerAnimatorManager playerAnimatorManager;
        [HideInInspector] public PlayerLocomotionManager playerLocomotionManager;
        [HideInInspector] public PlayerNetworkManager playerNetworkManager;
        [HideInInspector] public PlayerStatsManager playerStatsManager;

        protected override void Awake()
        {
            base.Awake();

            playerAnimatorManager = GetComponent<PlayerAnimatorManager>();
            playerLocomotionManager = GetComponent<PlayerLocomotionManager>();
            playerNetworkManager = GetComponent<PlayerNetworkManager>();
            playerStatsManager = GetComponent<PlayerStatsManager>();
        }

        protected override void Update()
        {
            base.Update();

            bool isLocalOwner = sceneTestingMode || IsOwner;
            if (!isLocalOwner)
                return;

            playerLocomotionManager.HandleAllMovement();

            playerStatsManager.RegenerateStamina();

            // Update UI manually in scene testing mode (since network callbacks won't fire)
            if (sceneTestingMode && PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHUDManager != null)
            {
                PlayerUIManager.instance.playerUIHUDManager.SetNewStaminaValue(0, playerNetworkManager.CurrentStaminaValue);
            }
        }

        protected override void LateUpdate()
        {
            bool isLocalOwner = sceneTestingMode || IsOwner;
            if (!isLocalOwner)
                return;

            base.LateUpdate();

            if (PlayerCamera.instance != null)
            {
                PlayerCamera.instance.HandleAllCameraActions();
            }
        }

        protected void Start()
        {
            // In scene testing mode, set up connections immediately
            if (sceneTestingMode)
            {
                SetupSceneTestingMode();
            }
        }

        private void SetupSceneTestingMode()
        {
            // Connect camera
            if (PlayerCamera.instance != null)
            {
                PlayerCamera.instance.player = this;
                // Debug.Log("Scene Testing: Camera connected to player");
            }
            else
            {
                Debug.LogWarning("Scene Testing: PlayerCamera.instance is NULL! Make sure camera has PlayerCamera component.");
            }

            // Connect input manager
            if (PlayerInputManager.instance != null)
            {
                PlayerInputManager.instance.player = this;
                // Debug.Log("Scene Testing: Input manager connected to player");
            }
            else
            {
                Debug.LogError("Scene Testing: PlayerInputManager.instance is NULL! Make sure PlayerInputManager exists in scene and is enabled.");
            }

            // Initialize stamina without network callbacks
            if (playerNetworkManager != null && playerStatsManager != null)
            {
                playerNetworkManager.MaxStaminaValue = playerStatsManager.CalculateStaminaBasedOnEnduranceLevel(playerNetworkManager.EnduranceValue);
                playerNetworkManager.CurrentStaminaValue = playerNetworkManager.MaxStaminaValue;
            }

            // Initialize UI in scene testing mode
            if (PlayerUIManager.instance != null && PlayerUIManager.instance.playerUIHUDManager != null)
            {
                PlayerUIManager.instance.playerUIHUDManager.SetMaxStaminaValue(playerNetworkManager.MaxStaminaValue);
                PlayerUIManager.instance.playerUIHUDManager.SetNewStaminaValue(0, playerNetworkManager.CurrentStaminaValue);
            }
            else
            {
                Debug.LogWarning("Scene Testing: PlayerUIManager or playerUIHUDManager is NULL!");
            }

        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                PlayerCamera.instance.player = this;
                PlayerInputManager.instance.player = this;
                WorldSaveGameManager.instance.player = this;

                playerNetworkManager.currentStamina.OnValueChanged +=
                    PlayerUIManager.instance.playerUIHUDManager.SetNewStaminaValue;
                playerNetworkManager.currentStamina.OnValueChanged += playerStatsManager.ResetStaminaRegenTimer;

                playerNetworkManager.MaxStaminaValue = playerStatsManager.CalculateStaminaBasedOnEnduranceLevel(playerNetworkManager.EnduranceValue);
                playerNetworkManager.CurrentStaminaValue = playerNetworkManager.MaxStaminaValue;
                PlayerUIManager.instance.playerUIHUDManager.SetMaxStaminaValue(playerNetworkManager.MaxStaminaValue);
            }
        }

        public void SaveGameDataToCurrentCharacterData(ref CharacterSaveData currentCharacterData)
        {
            currentCharacterData.sceneIndex = SceneManager.GetActiveScene().buildIndex;
            currentCharacterData.characterName = playerNetworkManager.CharacterNameValue.ToString();
            currentCharacterData.xPosition = transform.position.x;
            currentCharacterData.yPosition = transform.position.y;
            currentCharacterData.zPosition = transform.position.z;
        }

        public void LoadGameDataFromCurrentCharacterData(ref CharacterSaveData currentCharacterData)
        {
            playerNetworkManager.CharacterNameValue = currentCharacterData.characterName;
            var myPosition = new Vector3(currentCharacterData.xPosition, currentCharacterData.yPosition, currentCharacterData.zPosition);
            transform.position = myPosition;
        }
    }
}