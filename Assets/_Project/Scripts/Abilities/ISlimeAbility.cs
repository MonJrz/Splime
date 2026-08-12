namespace Splime.Abilities
{
    /// <summary>
    /// Interfaz común para todas las habilidades únicas de los Slimes.
    /// Permite activar y desactivar habilidades de forma modular y polimórfica en Netcode for GameObjects.
    /// </summary>
    public interface ISlimeAbility
    {
        /// <summary>
        /// Indica si la habilidad se encuentra actualmente activa.
        /// </summary>
        bool IsAbilityActive { get; }

        /// <summary>
        /// Ejecuta la activación de la habilidad.
        /// </summary>
        void ActivateAbility();

        /// <summary>
        /// Ejecuta la desactivación de la habilidad.
        /// </summary>
        void DeactivateAbility();

        /// <summary>
        /// Alterna el estado de la habilidad (activa <-> inactiva).
        /// </summary>
        void ToggleAbility();
    }
}
