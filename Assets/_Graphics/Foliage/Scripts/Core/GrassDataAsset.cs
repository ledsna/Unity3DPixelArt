using UnityEngine;

namespace Grass.Core
{
    public class GrassDataAsset : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private byte[] binaryData;

        public byte[] Data
        {
            get => binaryData;
            set => binaryData = value;
        }

        public int Length => binaryData?.Length ?? 0;
    }
}
