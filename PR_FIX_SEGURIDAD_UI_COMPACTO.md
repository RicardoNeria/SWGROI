# Hardening seguridad + UI compacto (sin ruptura)

## Resumen ejecutivo

Se fortaleció el backend (OWASP básico) y se compactó la interfaz sin alterar rutas, contratos JSON ni conexiones locales/VPS. Se añadieron cabeceras de seguridad, CSRF por sesión, cookies de sesión seguras con rotación en login/logout, rate limiting para intentos de acceso, y hash PBKDF2 con migración transparente de contraseñas. En el front se integró un pequeño script de seguridad (CSRF) y variables CSS para tipografía/espaciados alineadas al módulo de Avisos, logrando mayor densidad visual manteniendo legibilidad.

## Cambios por archivo

- `Program.cs`: comentario menor; sin cambios funcionales.
- `StaticServer.cs`: sin cambios de rutas; el middleware corre en `RequestRouter`.
- `RequestRouter.cs`: middleware ligero (cabeceras seguridad, requestId, sesión + CSRF para POST/PUT/DELETE) antes de enrutar. Rutas intactas.
- `Security/SessionManager.cs`: sesión en memoria + token CSRF por sesión; seteo de cookies `sessionid` (HttpOnly, SameSite=Strict, Secure si HTTPS/proxy) y `csrftoken` (lectura JS). Rotación en login/logout.
- `Security/PasswordHasher.cs`: PBKDF2 (100k iter.) con formato `PBKDF2$iter$salt$hash` para migración sin esquema.
- `Security/RateLimiter.cs`: rate limit simple en memoria (ventana deslizante) para login.
- `Utils/SecurityHeaders.cs`: aplica CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy.
- `Utils/Logger.cs`: logger mínimo thread-safe con requestId y redactado básico.
- `Utils/Validate.cs`: helpers NotNullOrEmpty, MaxLen, Regex, Email, Folio, OVSR3.
- `LoginController.cs`: reemplazo de parseo manual; rate limit; verificación de contraseña (migración PBKDF2 si aplica); rotación de sesión; mantiene respuesta `{exito, rol, nombre}` y cookies de compatibilidad (`usuario`, `rol`, `nombre`).
- `LogoutController.cs`: destruye sesión y expira cookies, redirige a login.
- `SesionController.cs`: lee usuario/rol desde la sesión (si existe) o cookies clásicas para compatibilidad.
- `wwwroot/Scripts/seguridad.js`: inyecta `X-CSRF-Token` en `fetch`/XHR; helpers ligeros.
- `wwwroot/Styles/estilo_general.css`: variables CSS globales (`--font-size-base:14px`, `--font-size-sm:12px`, `--line-base:1.45`, paddings compactos) y utilidades de tablas/formularios.
- `wwwroot/*.html`: carga de `/scripts/seguridad.js` con `defer` (no bloqueante). Sin cambios de rutas.

Nota: No se modificaron `app.config` ni nombres/formatos de conexión. `ConexionBD.cs` y selección de conexiones se mantienen intactas.

## Checklist de pruebas manuales

- Seguridad
  - [ ] Intentos de inyección SQL en Tickets/Ventas/Cotizaciones neutralizados (consultas parametrizadas o validación previa donde aplica).
  - [ ] POST/PUT/DELETE sin `X-CSRF-Token` devuelve 403 `{code:"csrf_invalid"}`.
  - [ ] Cookie `sessionid` con `HttpOnly` y `SameSite=Strict`; `Secure` presente si tras proxy HTTPS (o en entorno HTTPS).
  - [ ] Rate limiting de login: más de 10 intentos/5 min por IP responde 429.
  - [ ] Migración de contraseñas: si credencial coincide y estaba en texto plano, tras login exitoso se re-hash en DB.

- Flujo crítico (sin ruptura)
  - [ ] Login/logout funcionan y mantienen rutas/respuestas JSON previas.
  - [ ] Creación y consulta de tickets, avisos, ventas y reportes operan igual.
  - [ ] Conexiones a BD (localhost y VPS) intactas; `app.config` y `ConexionBD.cs` sin alteración de cadenas/puertos.

- UI/Accesibilidad
  - [ ] Tipografía y espaciados más compactos y homogéneos en `admin.html`, `tickets.html`, `ventas.html`, `reportes.html`, `tecnicos.html`.
  - [ ] Estados de foco visibles; contraste adecuado en botones/inputs.
  - [ ] Tablas con `overflow-x` en pantallas pequeñas; celdas con `ellipsis` y `title` cuando aplica.

## Rollback

1. Revertir PR/merge del branch `fix/seguridad-ui-compacto` (git revert o rebase según política).
2. Si hubo migración de contraseñas a formato `PBKDF2$...`, las credenciales seguirán funcionando (verificador admite tanto PBKDF2 como texto plano). No requiere acción adicional.
3. Remover/ignorar `/wwwroot/Scripts/seguridad.js` de las páginas HTML si se desea volver al comportamiento previo sin CSRF.
4. Eliminar middleware CSRF llamando directamente a controladores desde `RequestRouter` (revert del archivo).

## Evidencia sugerida

Adjuntar capturas antes/después de:
- Tipografía/densidad en `ventas.html` y `tickets.html`.
- Respuestas con cabeceras: `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`.

