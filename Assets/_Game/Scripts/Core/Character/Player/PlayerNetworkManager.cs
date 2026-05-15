using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

namespace Ledsna
{
    public class PlayerNetworkManager : CharacterNetworkManager
    {
        private FixedString64Bytes localCharacterName = "Character";

        public NetworkVariable<FixedString64Bytes> characterName = new NetworkVariable<FixedString64Bytes>("Character", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public FixedString64Bytes CharacterNameValue
        {
            get => UseLocalValues ? localCharacterName : characterName.Value;
            set
            {
                if (UseLocalValues)
                    localCharacterName = value;
                else if (IsSpawned)
                    characterName.Value = value;
            }
        }
    }
}