# Ensayo aislado de invalidación de accesos restaurados

Incorporado: 2026-10-25. Amplía la verificación del [backup cifrado](DATABASE_BACKUPS.md)
con una preparación de seguridad sobre su propia base temporal. No conserva un
destino recuperado ni sustituye la base habitual.

## Uso

Con Docker/PostgreSQL 16 disponibles, desde la raíz, en otra terminal:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\backup.ps1 -Action rehearse -File 'C:\ruta\copia.scaibackup'
```

Reemplazá la ruta por `file` de una copia existente e ingresá su contraseña en
el prompt oculto. No hace falta detener API/web. El helper compila en `BackupTools`
y envía la contraseña por stdin al hijo; no la escribas en argumentos, `.env`
o documentación. `create` y `verify` conservan sus comportamientos anteriores.

El ensayo autentica el archivo antes de crear una base. Restaura únicamente en
`scai_restore_test_<UUID>` generado internamente, restringe conexiones de PUBLIC
y compara esquema/conteos/SHA-256 con el inventario autenticado. Después aplica
la preparación descrita abajo. El archivo y la base original quedan intactos;
la copia temporal se retira al terminar, también ante fallo o cancelación.

No acepta un nombre de destino de restauración ni inicia API, worker, SMTP,
Data Protection o migraciones en el destino. `-DatabaseName` sigue reservado
exclusivamente a fixtures del script de pruebas; no elige el destino del ensayo.

## Transformaciones en la copia temporal

| Datos | Preparación |
| --- | --- |
| Sesiones y credenciales de renovación | Retirada completa; el JWT anterior pierde su sesión válida. |
| Tokens de verificación/recuperación | Retirada completa, incluidos consumidos o vencidos. |
| Códigos de vinculación y credenciales del agente | Retirada completa; equipos marcados como revocados. |
| Solicitudes de cambio de correo | Retirada completa; perfil/correo actual conservados. |
| Cola cifrada de correo | Retirada completa sin descifrarla ni reproducir mensajes. |
| Versiones de usuario/dispositivo | Nuevos UUID para impedir reutilizar versiones anteriores. |
| Contraseñas y contenido del producto | Conservados y comprobados mediante huellas. |

La política exige las 17 tablas públicas actuales, columnas/tipos/nulabilidad del
modelo compilado y última migración `20261025143028_DurableAccountEmailOutbox`.
Una tabla desconocida, columna adicional o migración no revisada detiene el ensayo.
Actualizar el esquema requiere revisar esta política. Una copia anterior puede
seguir siendo verificable con `verify` sin ser compatible con `rehearse`.

La preparación usa una transacción serializable y bloquea solamente las tablas
del destino temporal. Antes de escribir vuelve a comparar el inventario.
Después verifica las siete tablas de accesos vacías, todos los equipos revocados
y la conservación de ocho tablas completas: migraciones, informes, hallazgos,
feedback, reglas, marcas de borrado, cuota y actividad. Para usuarios/dispositivos
compara el JSON completo excluyendo solo las versiones y, en dispositivos, la
fecha de revocación. Así también comprueba hashes de contraseña, perfil y
relaciones. Una diferencia revierte la transacción y no produce éxito.

## Resultado y límites

El JSON devuelve `status: "rehearsed-in-isolation"`, ruta/fecha de la copia,
`tablesVerified` y `preparation` con fecha UTC, cantidades retiradas, versiones
renovadas y dos comprobaciones: `contentPreserved` y `accessInvalidationChecked`.
`contentTablesChecked` cuenta diez tablas/proyecciones. No devuelve filas,
tokens, payloads o credenciales. La ruta de archivo sigue siendo metadato local.

`activeDatabaseReplaced` y `laterDeletionsReconciled` son **false**. El resultado
no certifica recuperación del servicio: no concilia cuentas/informes borrados
después de la instantánea, no identifica un compromiso previo, no rota secretos
de infraestructura, no decide RPO/RTO y no prepara una copia externa.
Los hashes de contraseña permanecen; si hubo compromiso requieren evaluación
propia antes de permitir acceso a un servicio recuperado.

El límite de cinco minutos, memoria/archivo de 64 MiB, códigos de salida y retiro
con plazo independiente se conservan del helper de backups. Un cierre abrupto
puede impedir la limpieza; si se informa código 4, identificar el destino temporal
antes de retirarlo. Restaurar SQL de un administrador desconocido sigue fuera
del alcance: esta herramienta no es un sandbox para dumps maliciosos.

## Comprobación con datos ficticios

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test-backup.ps1
```

Ocho ensayos PostgreSQL: tres anteriores de backup y cinco nuevos de preparación.
Comprueban contenido conservado, archivo/origen intactos, accesos anteriores
rechazados por API/agente, nuevo login, rollback por fallo SQL o cambio de
contenido, destinos/esquemas desconocidos y limpieza. El script también recorre
create/verify/rehearse por CLI y una contraseña errónea. No usa datos personales.
Tres pruebas adicionales sin PostgreSQL comprueban guardas y contrato del informe.
Esta evidencia no sustituye las pruebas de Windows en otra PC/VM, pospuestas.

El [procedimiento operativo](RECOVERY_RUNBOOK.md) conserva las decisiones pendientes
para una recuperación real. Referencias: [transacciones de PostgreSQL 16](https://www.postgresql.org/docs/16/sql-set-transaction.html),
[bloqueos](https://www.postgresql.org/docs/16/sql-lock.html) y
[generación de UUID](https://www.postgresql.org/docs/16/functions-uuid.html).
