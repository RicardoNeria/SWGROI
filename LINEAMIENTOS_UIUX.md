# Lineamientos UI/UX

- Tipografía: Inter (o sistema como fallback). Tamaños: 12, 14, 16, 20, 24 px.
- Espaciado: escala de 8 puntos (4, 8, 16, 24, 32, 40, …).
- Paleta de color centralizada con variables CSS en `:root` (ver `wwwroot/Styles/estilo_general.css`).
- Iconografía: 20–24 px en botones primarios; 16 px en listas y acciones secundarias.
- Estados de interacción: foco, hover, disabled, error, success, warning. Usar clases y `aria-live` para accesibilidad.
- Componentes recomendados:
  - Botones primarios/secundarios consistentes.
  - Inputs con estados `.is-valid` y `.is-invalid`.
  - Toasts para mensajes breves; modales de confirmación para acciones destructivas.
- Accesibilidad:
  - Contraste mínimo AA.
  - `:focus-visible` en controles interactivos.
  - Mensajes con `aria-live="polite"`.
- Redacción:
  - Español formal, oraciones breves, imperativos claros.
  - Mensajes de error con causa y sugerencia.

## Variables CSS sugeridas

Ver archivo `wwwroot/Styles/estilo_general.css` con paleta y radios/espacios.

## Ejemplos de estados
- `.is-invalid` resalta borde y mensaje `<small class="feedback error">`.
- `.is-valid` muestra confirmación sutil.
- Botones con spinner durante cargas (`.btn--loading`).

