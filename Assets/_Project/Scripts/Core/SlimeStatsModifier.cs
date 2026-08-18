using UnityEngine;

namespace Splime.Core
{
    /// <summary>
    /// Fuente de verdad de stats en runtime para un Slime.
    /// Combina los valores base del SlimeData con multiplicadores temporales
    /// aplicados por las habilidades activas (PlayerSqueezeAbility, PlayerMetalFormAbility, etc.)
    /// 
    /// Arquitectura de dos capas:
    ///   Capa 1 (Stats):      SlimeData (base inmutable) × Multiplicadores → Stats finales
    ///   Capa 2 (Comportamiento): ISlimeAbility → lógica propia + setear multiplicadores aquí
    ///
    /// SlimeMovement y SlimeJump leen de aquí en lugar de SlimeData directamente.
    /// Las habilidades activas modifican los multiplicadores cuando se activan/desactivan.
    /// </summary>
    public class SlimeStatsModifier : MonoBehaviour
    {
        // Referencia a los stats base (inmutable)
        private SlimeData _baseData;

        // ── Multiplicadores de Stats ─────────────────────────────────────────
        // Valor 1.0 = sin modificación. Las habilidades cambian estos valores.

        /// <summary>Multiplicador de velocidad de movimiento. 1.0 = normal.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Multiplicador de fuerza de salto. 1.0 = normal. 2.0 = doble salto.</summary>
        public float JumpMultiplier { get; set; } = 1f;

        public float? JumpForceOverride { get; set; } = null;

        public float? MoveSpeedOverride { get; set; } = null;

        public float PushStrength { get; set; } = 0f;

        /// <summary>Multiplicador de gravedad. 1.0 = normal. Valores > 1 = más pesado.</summary>
        public float GravityMultiplier { get; set; } = 1f;

        // ── Stats Finales (base × multiplicador) ────────────────────────────

        /// <summary>Velocidad de movimiento final en runtime.</summary>
        public float MoveSpeed => MoveSpeedOverride ?? (_baseData != null ? _baseData.MoveSpeed * SpeedMultiplier : 6f * SpeedMultiplier);

        /// <summary>Velocidad de rotación (no modificable por habilidades aún).</summary>
        public float RotationSpeed => _baseData != null ? _baseData.RotationSpeed : 12f;

        /// <summary>Fuerza de salto final en runtime (puede ser multiplicada por habilidades).</summary>
        public float JumpForce => JumpForceOverride ?? (_baseData != null ? _baseData.JumpForce * JumpMultiplier : 8f * JumpMultiplier);

        /// <summary>Gravedad final en runtime.</summary>
        public float Gravity => _baseData != null ? _baseData.Gravity * GravityMultiplier : -20f * GravityMultiplier;

        /// <summary>Indica si el SlimeStatsModifier fue inicializado con datos base.</summary>
        public bool IsInitialized => _baseData != null;

        // ── Inicialización ──────────────────────────────────────────────────

        /// <summary>
        /// Inicializa el modificador con los stats base del SlimeData.
        /// Llamado por NetworkGameManager al spawnear el Slime.
        /// </summary>
        public void Initialize(SlimeData baseData)
        {
            _baseData = baseData;
            ResetMultipliers();
            Debug.Log($"[{nameof(SlimeStatsModifier)}] ✅ Inicializado para {gameObject.name} " +
                      $"| Speed: {MoveSpeed} | Jump: {JumpForce} | Gravity: {Gravity}");
        }

        /// <summary>
        /// Resetea todos los multiplicadores a sus valores neutros (1.0).
        /// Útil al desactivar una habilidad para volver al estado normal.
        /// </summary>
public void ResetMultipliers()
        {
            SpeedMultiplier   = 1f;
            JumpMultiplier    = 1f;
            GravityMultiplier = 1f;
            JumpForceOverride = null;
            MoveSpeedOverride = null;
            PushStrength      = 0f;
        }
    }
}
