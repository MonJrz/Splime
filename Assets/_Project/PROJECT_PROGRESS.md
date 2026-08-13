# Splime - Estado del Proyecto y Progreso (Fase 2 Completada — Online Multiplayer Activo)

**ID de Conversación Fase 1**: `cd184943-2a76-4d43-90c4-7d01bc673e5c`
**ID de Conversación Fase 2**: `390494b5-e69d-469e-8373-6369668c606e`

> ⚠️ **RUTA ACTIVA DEL PROYECTO**: `C:\Splime`
> (Movido desde `C:\Users\Darlin\Desktop\Splime` para evitar conflictos de permisos EPERM con UnityPackageManager en Windows 11)

---

## 📌 Resumen de lo Construido Hasta Ahora

### **Paso 1: Arquitectura Definida**
- **Netcode for GameObjects (NGO)** con arquitectura Host/Client.
- **Física**: `CharacterController` + `NetworkTransform` para movimiento top-down/isométrico preciso.
- **Habilidades Modulares**: Basadas en datos (`SlimeData`) e Interfaz (`ISlimeAbility`) sin condicionales repetidos `if (isSlime2)`.
- **Cámara Cooperativa**: Totalmente local por cliente (punto medio entre P1 y P2 con zoom dinámico).

---

### **Paso 2: Estructura de Datos y Prefabs**
- ScriptableObject [`Assets/_Project/Scripts/Core/SlimeData.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Core/SlimeData.cs).
- Assets creados en `Assets/_Project/Settings/`:
  - `SlimeData_Transformer` (Salto estándar: 7.5)
  - `SlimeData_Agile` (Salto superior: 10.5)
- Prefabs creados en `Assets/_Project/Prefabs/Players/`:
  - `Slime_Transformer.prefab` (`CharacterController` + `NetworkObject` + `NetworkTransform` + `SlimeInput` + `SlimeMovement`)
  - `Slime_Agile.prefab` (`CharacterController` + `NetworkObject` + `NetworkTransform` + `SlimeInput` + `SlimeMovement`)
- Objeto de red en escena: `[NetworkManager]` con `UnityTransport` y `NetworkPrefabsList`.

---

### **Paso 3: Input System & Control de Red**
- Script [`Assets/_Project/Scripts/Player/SlimeInput.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Player/SlimeInput.cs).
- Conectado a [`Assets/InputSystem_Actions.inputactions`](file:///C:/Users/Darlin/Desktop/Splime/Assets/InputSystem_Actions.inputactions).
- Acciones leídas: `Move` (WASD / Vector2), `Jump` (Espacio), `Ability` (Tecla F / Tap), `Interact` (Tecla E).
- **Protección de Red**: Desactiva lectura si `!IsOwner` para evitar que un cliente controle el Slime de otro jugador.

---

### **Paso 4: Movimiento 3D Isométrico**
- Script [`Assets/_Project/Scripts/Player/SlimeMovement.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Player/SlimeMovement.cs).
- Movimiento 3D proyectado en el plano horizontal de la vista de la cámara.
- Rotación suave hacia la dirección de avance.
- Aceleración y desaceleración fluida estilo Slime.

---

### **Paso 5: Salto Multiplayer Autoritativo**
- Script [`Assets/_Project/Scripts/Player/SlimeJump.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Player/SlimeJump.cs).
- Salto sincronizado localmente y replicado por `NetworkTransform`.
- Fuerza de salto leída automáticamente desde `SlimeData`:
  - **Slime Transformador**: Salto estándar (`7.5`).
  - **Slime Ágil**: Salto superior (`10.5`).

---

### **Paso 6: Sistema Base de Habilidades**
- Interfaz [`Assets/_Project/Scripts/Abilities/ISlimeAbility.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Abilities/ISlimeAbility.cs).
- Script [`Assets/_Project/Scripts/Abilities/SlimeAbilityController.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Abilities/SlimeAbilityController.cs).
- Arquitectura modular y desacoplada mediante interfaz C# para activar/desactivar cualquier habilidad al presionar `Ability` (Tecla F / Tap).

---

### **Paso 7: Habilidad del Slime Transformador**
- Script [`Assets/_Project/Scripts/Abilities/TransformAbility.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Abilities/TransformAbility.cs).
- Implementa `ISlimeAbility` para cambiar de forma (Normal <-> Plataforma).
- Sincronización en red de la forma activa mediante `NetworkVariable<int>` (`_currentFormIndex`).
- Modifica dinámicamente la escala visual (`localScale`) y el `CharacterController` (`height` y `center`).

---

### **Paso 8: Habilidad del Slime Ágil**
- Script [`Assets/_Project/Scripts/Abilities/AgileAbility.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Abilities/AgileAbility.cs).
- Implementa `ISlimeAbility` para encogerse ("Modo Escurrirse") y atravesar zonas estrechas y tuberías.
- Sincronización en red mediante `NetworkVariable<bool>` (`_isAgileModeActive`).
- Reajusta dinámicamente la escala (`(0.5, 0.5, 0.5)`) y las dimensiones del `CharacterController` (`height = 0.6`, `radius = 0.25`).

---

### **Paso 9: Cámara Cooperativa Local**
- Script [`Assets/_Project/Scripts/Camera/CooperativeCamera.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Camera/CooperativeCamera.cs).
- 100% Local en la pantalla de cada cliente (sin consumo de ancho de banda de red).
- Auto-detecta y calcula el punto medio (`GetCenterPoint`) entre ambos Slimes.
- Zoom dinámico basado en la distancia horizontal relativa entre los dos jugadores.
- Movimiento suave mediante `Vector3.SmoothDamp`.

---

### **Paso 10: Gestor de Red y Pruebas Multijugador (Completado)**
- Script [`Assets/_Project/Scripts/Network/NetworkGameManager.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Network/NetworkGameManager.cs).
- Configuración de **`AuthorityMode = Owner`** en el componente estándar `NetworkTransform` de ambos prefabs (`Slime_Transformer` y `Slime_Agile`) para permitir que el Cliente (P2) mueva su personaje y replique la posición al Host.
- Clonación aislada del asset de Input en [`Assets/_Project/Scripts/Player/SlimeInput.cs`](file:///C:/Users/Darlin/Desktop/Splime/Assets/_Project/Scripts/Player/SlimeInput.cs) para evitar que deshabilitar el mapa de un jugador desactive el input del otro.
- Gestión autoritativa de conexión Host/Cliente.
- Spawnea automáticamente a **`Slime_Transformer`** para el Jugador 1 (Host / Client 0) y a **`Slime_Agile`** para el Jugador 2 (Client 1) asignando Ownership estricto.
- Incluye controles de prueba OnGUI en pantalla (`Start Host` / `Start Client`).

---

## 🎉 ¡TODOS LOS PASOS (1 AL 10) HAN SIDO COMPLETADOS CON ÉXITO!



```text
              GAME
                │
       ┌────────┴────────┐
       │                 │
    Player 1          Player 2
    Slime A           Slime B
    (Transformer)     (Ágil)
       │                 │
       ▼                 ▼
     WASD              WASD
       │                 │
       ▼                 ▼
    Movimiento       Movimiento
    Salto (7.5)      Salto (10.5)
    Plataforma       Escurrirse
       │                 │
       └────────┬────────┘
                │
           NGO Network
                │
                ▼
       Estado Sincronizado
```

---

## 🌐 FASE 2 — Online Multiplayer Real por Internet (Unity Relay)

**Completado el**: 2026-08-12
**Versiones usadas**:
- Unity: 6000.3.16f1
- Netcode for GameObjects: 2.13.1
- Unity Transport: 2.7.3
- Multiplayer Services SDK: 2.3.0

### Paso 2-F2: Inicialización de Unity Gaming Services + Autenticación Anónima
- Integrado en `NetworkGameManager.cs` (método `InitializeServicesAsync()`).
- Llama a `UnityServices.InitializeAsync()` y `AuthenticationService.Instance.SignInAnonymouslyAsync()`.
- Reutiliza token cacheado si el jugador ya estaba autenticado previamente.
- Si UGS falla (Project ID no vinculado, sin internet), muestra error descriptivo en consola.

### Paso 3-F2 & 4-F2: Creación de Sesión como Host (Unity Relay)
- Método `StartHostWithRelayAsync()` en `NetworkGameManager.cs`.
- Usa `MultiplayerService.Instance.CreateSessionAsync(options)` con `MaxPlayers = 2` y `.WithRelayNetwork()`.
- Unity Relay asigna automáticamente servidores en la nube según la región óptima (QoS automático).
- Genera y muestra en pantalla el **JOIN CODE** (6 caracteres) para compartir con el Cliente.
- NGO se inicia automáticamente como Host vinculado al Relay.

### Paso 5-F2: Conexión de Cliente por Join Code (Unity Relay)
- Método `JoinSessionWithRelayAsync(string codeToJoin)` en `NetworkGameManager.cs`.
- UI OnGUI actualizada con campo de texto para ingresar el Join Code.
- Usa `MultiplayerService.Instance.JoinSessionByCodeAsync(formattedCode)`.
- El código se normaliza automáticamente a mayúsculas antes de enviarlo.

### Paso 9-F2: Desconexión Limpia (UGS + NGO)
- Método `DisconnectAsync()` en `NetworkGameManager.cs`.
- Orden de cierre correcto: `_currentSession.LeaveAsync()` → `NetworkManager.Singleton.Shutdown()` → limpieza de estado local.
- Corregida la advertencia NGO `"Attempted despawn before NetworkObject was spawned"` añadiendo validación `netObj.IsSpawned` antes del despawn manual en `OnClientDisconnected`.

### ✅ Resultado de Prueba Final
- **Prueba local (mismo equipo):** ✅ Exitosa.
- **Prueba real entre dos PCs en redes distintas (Internet):** ✅ **EXITOSA con JOIN CODE `PWNDH8`**.
- Spawn de Slimes, Ownership, habilidades (Plataforma / Escurrirse) y cámara cooperativa funcionando correctamente en conexión online real.

### Nota de Infraestructura
- Proyecto movido de `C:\Users\Darlin\Desktop\Splime` a `C:\Splime` para resolver errores `EPERM: operation not permitted` del `UnityPackageManager` causados por políticas de permisos de Windows 11 sobre carpetas de usuario.

---

## 🚀 PRÓXIMOS PASOS SUGERIDOS (Fase 3)

- Diseño e implementación del Nivel 1 completo con puzzles cooperativos.
- Sistema de diálogos / narrativa del laboratorio.
- UI de juego definitiva (reemplazar el debug OnGUI).
- Sistema de victoria/derrota y reinicio de nivel.
- Sonido y efectos visuales para las habilidades.
