using Splime.Player;
using UnityEngine;

namespace Splime.Collectibles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class CosmeticOcclusionZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            PlayerCosmeticController cosmetics =
                other.GetComponentInParent<PlayerCosmeticController>();

            cosmetics?.PushHeadOcclusion();
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerCosmeticController cosmetics =
                other.GetComponentInParent<PlayerCosmeticController>();

            cosmetics?.PopHeadOcclusion();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
#endif
    }
}