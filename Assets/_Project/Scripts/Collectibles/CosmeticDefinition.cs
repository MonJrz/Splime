using UnityEngine;

namespace Splime.Collectibles
{
    [CreateAssetMenu(
        fileName = "CosmeticDefinition",
        menuName = "Splime/Cosmetics/Cosmetic Definition")]
    public class CosmeticDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private CosmeticId _id = CosmeticId.None;
        [SerializeField] private CosmeticSlot _slot;

        [Header("Visual")]
        [SerializeField] private GameObject _prefab;

        public CosmeticId Id => _id;
        public CosmeticSlot Slot => _slot;
        public GameObject Prefab => _prefab;
    }
}