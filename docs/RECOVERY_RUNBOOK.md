# Recuperación operativa: procedimiento inicial

Documento de preparación, actualizado 2026-10-25. **No se ejecutó una recuperación sobre la
base activa.** El helper verifica copias y permite ensayar la invalidación de
accesos en bases temporales que elimina. Este procedimiento no convierte ese ensayo en un restablecimiento
del servicio ni establece plazos de conservación o disponibilidad.

## Decisiones que deben registrarse antes de operar con usuarios externos

| Decisión | Estado actual |
| --- | --- |
| Pérdida de datos admisible (RPO) | Sin acordar. Registrar cuánto tiempo de cambios puede perderse y cómo se mide desde la instantánea. |
| Tiempo máximo de recuperación (RTO) | Sin acordar. Medir desde el incidente hasta las comprobaciones que permiten reabrir. |
| Operador y reemplazo | Sin designar. Necesitan acceso separado al entorno, a las copias y a la contraseña. |
| Custodia y copia externa | Pendientes. Un archivo en el mismo equipo no cubre pérdida/compromiso de ese equipo. |
| Borrados posteriores a la copia | No existe registro externo durable para reconstruirlos. Impide prometer que una restauración antigua no reintroduzca datos borrados. |
| Accesos restaurados | Invalidación transaccional comprobable con `-Action rehearse`, exclusivamente en la copia temporal. Falta incorporarla a una recuperación real antes de reabrir. [Alcance](RECOVERY_REHEARSAL.md). |

No se eligen valores de producción para RPO/RTO o conservación por inferencia.
La evidencia de un ensayo debe indicar tiempos observados y límites, separados
de esos objetivos todavía pendientes.

## Contener y elegir una copia

1. Registrar hora UTC, síntoma, alcance y operador, sin copiar contraseñas,
   tokens, evidencias personales o correos a un ticket. Conservar el estado
   original para análisis y detener nuevas escrituras/envíos al entorno afectado.
2. Distinguir fallo de almacenamiento y posible compromiso. Una copia posterior
   al inicio de un compromiso puede contener cambios del atacante. Preservar
   evidencia y elegir un entorno limpio antes de reutilizar datos o secretos.
3. Identificar el archivo cifrado, fecha UTC de su instantánea y versión de
   aplicación/esquema. Comprobar que existe la contraseña de la copia y acceso a
   los archivos necesarios. La clave no está en el repositorio ni puede derivarse
   de `JWT_KEY` o de la contraseña de PostgreSQL.
4. Comprobar integridad y restaurabilidad con el helper existente, en otra terminal:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\backup.ps1 -Action verify -File 'C:\ruta\copia.scaibackup'
   ```

   El nombre/ruta es un ejemplo; usar el archivo real mostrado al crear la copia.
   La contraseña se ingresa en el prompt, nunca como argumento. El formato
   autenticado y el inventario permiten verificar descifrado y una restauración
   temporal. No se sustituye la base activa y la temporal se elimina al terminar.
5. Registrar resultado, tablas comprobadas y duración del ensayo. Una contraseña
   incorrecta o inventario distinto detiene el procedimiento. No borrar la copia
   o el volumen original para resolver el fallo.

Para comprobar además la retirada de accesos sobre una copia compatible, usar
`-Action rehearse` con el mismo `-File`. Verifica el inventario, invalida sesiones,
enlaces, agentes, solicitudes de correo y cola, y comprueba conservación del
contenido. La temporal se elimina: el resultado no es un destino listo para
reabrir. [Uso, política de esquema y resultado](RECOVERY_REHEARSAL.md).

## Preparar la recuperación real fuera del servicio activo

La herramienta actual **no ofrece una orden de restauración real**. Antes de
incorporarla hacen falta un destino separado y verificable, acceso administrativo
mínimo, espacio disponible, custodia del descifrado y plan de retorno al entorno
anterior. No apuntar la conexión de la API a una copia sin completar los controles.

En ese destino aislado se deben comprobar esquema y migraciones, conteos y
relaciones del inventario. Después, antes de habilitar tráfico o SMTP:

- Invalidar todas las sesiones web y sus credenciales de renovación; cambiar
  versiones para impedir escrituras con estado anterior. Limpiar cookies de un
  navegador no invalida las copias de los tokens en otros equipos.
- Consumir o retirar todos los tokens de verificación/recuperación restaurados.
  Los correos antiguos podrían seguir existiendo fuera de la base.
- Retirar la cola `account_email_outbox` restaurada antes de iniciar el worker o
  habilitar SMTP. Sus claves tienen custodia independiente y no están en el dump;
  aunque puedan descifrarse, no corresponde reproducir enlaces/avisos anteriores
  a la recuperación. El ensayo de backup no inicia el worker sobre la copia.
- Invalidar códigos de vinculación y credenciales de agentes restaurados. Los
  clientes deberán vincularse de nuevo. Rotar solo la clave JWT no cierra ese
  acceso, que usa autorización independiente.
- Decidir rotación de secretos de infraestructura según el incidente y distribuir
  los reemplazos sin conservar secretos comprometidos en la copia de configuración.
  Cambiar el archivo `.env` no cambia por sí solo un rol ya creado en PostgreSQL.
- Reconciliar borrados de cuentas, análisis y feedback posteriores a la instantánea.
  Las marcas mínimas incluidas en la propia copia no conocen los borrados futuros.
  Si no se pueden reconstruir, registrar la limitación y mantener ese contenido
  fuera de exposición hasta resolver el tratamiento.

La retirada de accesos y conservación de contenido ya tienen helper transaccional
y pruebas de rollback/aislamiento para el ensayo temporal. La recuperación real
todavía requiere su destino, conciliación de borrados y controles operativos.
No se incluyen sentencias de reemplazo de la base activa ni se ejecutan sobre
los datos del usuario en esta entrega.

## Ensayo y reapertura

Usar cuentas ficticias para comprobar que JWT, refresh, enlaces y credenciales
anteriores son rechazados, que el nuevo login/vinculación funciona y que una
cuenta no accede a otra. Comprobar contenido, exportaciones y borrados conciliados,
readiness y correo en capturador aislado. Un 200 de readiness solo prueba la
consulta limitada de PostgreSQL, no estos controles.

Registrar versión y archivo ensayados, tiempos, comprobaciones, fallos y limpieza.
Conservar el entorno anterior separado hasta verificar el recuperado. Definir
cuándo repetir el ensayo después de cambios de esquema/seguridad y quién revisa
su evidencia. Programación, plazos y una primera recuperación completa siguen
pendientes. [Copias actuales](DATABASE_BACKUPS.md), [monitoreo](OPERATIONAL_MONITORING.md)
y [pendientes](PENDING_FEATURES.md).
