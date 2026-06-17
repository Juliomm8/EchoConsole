# Unity Session Event Contract

## Echo Console / Cosmic Diner

Version: 1.0
Sprint: S6.5 Unity Event Contract Hardening

## 1. Propósito

Este documento define las reglas oficiales para enviar eventos de gameplay desde el cliente Unity de Cosmic Diner hacia Echo Console.

El envío de telemetría es secundario al gameplay. Ninguna solicitud de telemetría puede bloquear el hilo principal, pausar mecánicas, detener una escena o congelar la experiencia del jugador.

## 2. Endpoint

```http
POST /api/client/sessions/{sessionId}/events
```

Ejemplo:

```http
POST https://api.example.com/api/client/sessions/5b73a4ca-da2f-4456-8688-b7dc1aa54cd2/events
```

## 3. Headers obligatorios

```http
Content-Type: application/json
Accept: application/json
X-Session-Token: SESSION_TOKEN
```

El `X-Session-Token` es devuelto por el endpoint de inicio de sesión.

No debe:

* imprimirse en logs;
* almacenarse en telemetría;
* enviarse a EchoConsole.Web;
* guardarse en archivos de texto;
* exponerse al jugador.

## 4. Tipos de evento oficiales

Los únicos valores permitidos para `eventType` son:

| EventType          | Uso                                    |
| ------------------ | -------------------------------------- |
| `SceneChanged`     | El jugador cambió de escena            |
| `PhaseChanged`     | Cambió la fase lógica de la partida    |
| `GameStateChanged` | Cambió el estado global del juego      |
| `ObjectiveUpdated` | Se actualizó un objetivo               |
| `ItemCollected`    | El jugador recogió un objeto relevante |
| `EnemyEncountered` | El jugador encontró un enemigo         |
| `PlayerDamaged`    | El jugador recibió daño                |
| `PlayerDied`       | El jugador murió                       |

El servidor acepta diferencias de mayúsculas y minúsculas, pero siempre guarda el nombre canónico.

Ejemplo:

```text
scenechanged
```

se almacena como:

```text
SceneChanged
```

No se permiten eventos arbitrarios como:

```text
Custom
Debug
Test
Heartbeat
SessionStarted
SessionEnded
```

El inicio, heartbeat y final de sesión tienen endpoints propios.

## 5. Request

```json
{
  "eventType": "SceneChanged",
  "scene": "Diner_Main",
  "gameState": "Gameplay",
  "phase": "Exploration",
  "payloadJson": "{\"fromScene\":\"MainMenu\",\"toScene\":\"Diner_Main\"}",
  "clientTimeUtc": "2026-06-17T15:30:00Z"
}
```

## 6. Límites de campos

| Campo            |           Límite |
| ---------------- | ---------------: |
| `eventType`      |    64 caracteres |
| `scene`          |   128 caracteres |
| `gameState`      |    64 caracteres |
| `phase`          |    64 caracteres |
| `payloadJson`    |  4000 caracteres |
| `payloadJson`    | 8192 bytes UTF-8 |
| Request completo |      32768 bytes |
| Profundidad JSON |       16 niveles |

El payload debe cumplir ambos límites:

```text
Máximo 4000 caracteres
Máximo 8192 bytes UTF-8
```

Cumplir un límite no anula el otro.

## 7. Reglas de PayloadJson

`payloadJson` es opcional.

Cuando se envía:

* debe ser JSON válido;
* la raíz debe ser un objeto;
* no puede ser un array;
* no puede ser un string aislado;
* no permite comentarios;
* no permite comas finales;
* no puede superar profundidad 16;
* no debe contener información personal;
* no debe contener tokens, claves o credenciales.

Válido:

```json
{
  "itemId": "ITEM_FLASHLIGHT",
  "quantity": 1,
  "room": "Storage"
}
```

Inválido:

```json
[
  "ITEM_FLASHLIGHT"
]
```

Inválido:

```json
"ITEM_FLASHLIGHT"
```

## 8. Rate Limit

El endpoint permite:

```text
120 eventos por minuto por SessionId
```

La política usa una ventana deslizante de 60 segundos dividida en seis segmentos.

No existe cola en el servidor. Una solicitud que excede el límite recibe:

```http
429 Too Many Requests
```

Respuesta:

```json
{
  "code": "rate_limit_exceeded",
  "message": "The request rate limit has been exceeded.",
  "retryAfterSeconds": 8
}
```

El servidor puede incluir:

```http
Retry-After: 8
```

Unity debe respetar este valor.

## 9. Respuesta exitosa

```http
200 OK
```

```json
{
  "id": 125,
  "sessionId": "5b73a4ca-da2f-4456-8688-b7dc1aa54cd2",
  "eventType": "SceneChanged",
  "scene": "Diner_Main",
  "gameState": "Gameplay",
  "phase": "Exploration",
  "createdAtUtc": "2026-06-17T15:30:01Z",
  "serverTimeUtc": "2026-06-17T15:30:01Z"
}
```

## 10. Manejo de códigos HTTP

| Código | Significado                  | Acción de Unity                           |
| -----: | ---------------------------- | ----------------------------------------- |
|    200 | Evento guardado              | Eliminarlo de la cola local               |
|    400 | Evento o payload inválido    | No reintentar el mismo evento             |
|    401 | Token ausente o inválido     | Detener envíos e iniciar nueva sesión     |
|    404 | Sesión no encontrada         | Descartar cola de esa sesión              |
|    409 | Sesión finalizada o expirada | No enviar más eventos                     |
|    413 | Request demasiado grande     | Reducir payload; no repetir el mismo body |
|    429 | Rate limit excedido          | Esperar `Retry-After`                     |
|    500 | Error del servidor           | Reintentar con backoff                    |
|    503 | Servicio no disponible       | Reintentar con backoff                    |

## 11. Política de retry

### Errores de red, 500 y 503

Usar backoff:

```text
Intento 1: 2 segundos
Intento 2: 5 segundos
Intento 3: 10 segundos
```

Máximo recomendado:

```text
3 reintentos
```

Agregar un jitter pequeño para evitar que varios clientes reintenten exactamente al mismo tiempo.

Ejemplo:

```text
5 segundos + valor aleatorio entre 0 y 1 segundo
```

### Respuesta 429

No usar el backoff genérico.

Unity debe:

1. Leer `Retry-After`.
2. Esperar ese tiempo.
3. Reintentar solo si la sesión continúa activa.
4. Evitar reenviar eventos obsoletos.

### Respuestas 400, 401, 404, 409 y 413

No reintentar automáticamente el mismo evento.

## 12. Envío asíncrono obligatorio

Las solicitudes deben ejecutarse fuera del flujo crítico del gameplay.

No se permite:

```csharp
task.Wait();
task.Result;
while (!request.isDone) { }
```

No se debe bloquear:

* `Update`;
* `FixedUpdate`;
* carga de escenas;
* interacción del jugador;
* IA;
* animaciones;
* sistema de guardado;
* pausa;
* cierre de una cinemática.

El cliente debe utilizar:

* `UnityWebRequest` mediante coroutine;
* una cola asíncrona;
* o un método `async` compatible con la arquitectura de Unity.

## 13. Cola local recomendada

La cola debe ser limitada.

Valor recomendado:

```text
100 eventos pendientes
```

Cuando se llena:

1. Conservar eventos críticos como `PlayerDied`.
2. Descartar eventos antiguos de baja prioridad.
3. Unificar cambios repetitivos.
4. No aumentar memoria indefinidamente.

Ejemplo de eventos que pueden unificarse:

```text
GameStateChanged
PhaseChanged
SceneChanged
```

Si existen varios cambios pendientes, puede conservarse únicamente el más reciente cuando el orden intermedio ya no tenga valor analítico.

## 14. Prioridades sugeridas

Alta:

```text
PlayerDied
EnemyEncountered
ObjectiveUpdated
```

Media:

```text
ItemCollected
PlayerDamaged
SceneChanged
```

Baja:

```text
PhaseChanged
GameStateChanged
```

La prioridad solo controla la cola local. No cambia el formato HTTP.

## 15. Reglas de tiempo

`clientTimeUtc` debe enviarse en UTC usando formato ISO 8601.

Ejemplo:

```text
2026-06-17T15:30:00Z
```

El tiempo oficial de persistencia es `CreatedAtUtc`, generado por el servidor.

`clientTimeUtc` es informativo y no reemplaza el tiempo del servidor.

## 16. Seguridad

El payload no debe contener:

* nombre real del usuario;
* correo electrónico;
* dirección IP;
* contraseñas;
* API Keys;
* Session Tokens;
* rutas privadas del sistema;
* archivos locales;
* información personal.

## 17. Ejemplos por evento

### SceneChanged

```json
{
  "eventType": "SceneChanged",
  "scene": "Diner_Main",
  "gameState": "Gameplay",
  "phase": "Exploration",
  "payloadJson": "{\"fromScene\":\"MainMenu\",\"toScene\":\"Diner_Main\"}",
  "clientTimeUtc": "2026-06-17T15:30:00Z"
}
```

### ItemCollected

```json
{
  "eventType": "ItemCollected",
  "scene": "Storage",
  "gameState": "Gameplay",
  "phase": "Exploration",
  "payloadJson": "{\"itemId\":\"ITEM_FLASHLIGHT\",\"quantity\":1}",
  "clientTimeUtc": "2026-06-17T15:32:00Z"
}
```

### PlayerDamaged

```json
{
  "eventType": "PlayerDamaged",
  "scene": "MainHall",
  "gameState": "Gameplay",
  "phase": "Encounter",
  "payloadJson": "{\"source\":\"Astronaut\",\"damage\":25}",
  "clientTimeUtc": "2026-06-17T15:35:00Z"
}
```

### PlayerDied

```json
{
  "eventType": "PlayerDied",
  "scene": "MainHall",
  "gameState": "GameOver",
  "phase": "Death",
  "payloadJson": "{\"cause\":\"Astronaut\",\"runSeconds\":842}",
  "clientTimeUtc": "2026-06-17T15:36:00Z"
}
```

## 18. Regla principal

La telemetría nunca debe afectar la experiencia del jugador.

Si Echo Console está caído:

```text
Cosmic Diner debe seguir funcionando normalmente.
```
