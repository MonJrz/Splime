using Splime.Abilities;
using Splime.Player;
using UnityEngine;

namespace Splime.Collectibles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class NarrowPassageZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            PlayerCosmeticController cosmetics =
                other.GetComponentInParent<PlayerCosmeticController>();

            cosmetics?.PushHeadOcclusion();


            PlayerSqueezeAbility squeezeAbility =
                other.GetComponentInParent<PlayerSqueezeAbility>();

            squeezeAbility?.PushNormalFormBlock();
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerCosmeticController cosmetics =
                other.GetComponentInParent<PlayerCosmeticController>();

            cosmetics?.PopHeadOcclusion();


            PlayerSqueezeAbility squeezeAbility =
                other.GetComponentInParent<PlayerSqueezeAbility>();

            squeezeAbility?.PopNormalFormBlock();
        }

#if UNITY_EDITOR
        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }
#endif
    }
}