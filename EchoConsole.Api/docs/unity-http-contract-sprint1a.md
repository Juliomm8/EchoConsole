# EchoConsole - Contrato HTTP Unity (Sprint 1A)

**Versión:** 1.0 (Congelada)
**URL Base (Desarrollo Local):** `http://localhost:5047`
**URL Base (Remoto/Ngrok):** `https://[ID_ASIGNADO].ngrok-free.dev`

---

## Reglas de Integración (Unity)
1. **Registro:** Ejecutar `register` una sola vez al abrir el juego (si es la primera vez o si cambió la versión).
2. **Inicio de Sesión:** Ejecutar `start` al entrar al menú principal o iniciar partida. Guardar en memoria el `sessionId` y `sessionToken` que devuelve el servidor.
3. **Heartbeat (Latido):** Enviar el `heartbeat` exactamente cada 15 segundos usando el `sessionId` en la URL y el `sessionToken` en los Headers.
4. **Cierre:** Enviar el `end` en el evento `OnApplicationQuit()` de Unity.

---

## Endpoints

### 1. Registrar Instalación
* **Ruta:** `POST /api/client/installations/register`
* **Headers:** `Content-Type: application/json`
* **Body:**
{
  "installationId": "GUID-DEL-CLIENTE",
  "gameCode": "cosmic-diner",
  "buildVersion": "0.1.0-dev",
  "platform": "WindowsPlayer",
  "deviceName": "ASUS-F15",
  "deviceModel": "Desktop",
  "operatingSystem": "Windows 11"
}

### 2. Iniciar Sesión
* **Ruta:** `POST /api/client/sessions/start`
* **Headers:** `Content-Type: application/json`
* **Body:**
{
  "installationId": "GUID-DEL-CLIENTE",
  "gameCode": "cosmic-diner",
  "buildVersion": "0.1.0-dev",
  "currentScene": "MainMenu",
  "currentGameState": "Menu",
  "currentPhase": "Boot"
}

### 3. Enviar Heartbeat
* **Ruta:** `POST /api/client/sessions/{sessionId}/heartbeat`
* **Headers:** * `Content-Type: application/json`
  * `X-Session-Token: [TOKEN_DEVUELTO_POR_START]`
* **Body:**
{
  "currentScene": "RestaurantMain",
  "currentGameState": "Playing",
  "currentPhase": "PowerRestored",
  "clientTimeUtc": "2026-05-07T18:30:00Z"
}

### 4. Finalizar Sesión (Cierre Normal)
* **Ruta:** `POST /api/client/sessions/{sessionId}/end`
* **Headers:** * `Content-Type: application/json`
  * `X-Session-Token: [TOKEN_DEVUELTO_POR_START]`
* **Body:**
{
  "reason": "ApplicationQuit",
  "currentScene": "RestaurantMain",
  "currentGameState": "Paused",
  "currentPhase": "PowerRestored",
  "clientTimeUtc": "2026-05-07T18:40:00Z"
}