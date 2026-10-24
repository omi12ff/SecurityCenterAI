# Contraseñas nuevas y capacidad de sesiones

Implementado el 2026-10-24. Se aplica en la API y la web, sin cambiar contraseñas,
hashes o sesiones existentes al iniciar la aplicación. No requiere migraciones.

## Contraseñas nuevas

Registro, recuperación y cambio autenticado exigen al menos 15 puntos de código
Unicode y un máximo de 128 unidades UTF-16, límite histórico del contrato. Un
emoji no cuenta como dos caracteres para el mínimo. Se permiten espacios,
caracteres internacionales, frases largas y pegar desde un gestor. No se exigen
combinaciones de mayúsculas, números o símbolos, ni se impone caducidad periódica.
No se recortan, normalizan, truncan o cambian las claves antes de guardarlas.

La API también compara el valor completo con una lista local inicial de 23
contraseñas previsibles, sin distinguir mayúsculas en esa comparación. La lista
incluye ejemplos comunes y el nombre del producto; **no es una base completa de
filtraciones ni detecta todas las contraseñas comprometidas**. No se realiza una
consulta externa, se guarda el candidato o se incorpora su valor a errores/logs.
La web comprueba longitud; la lista la decide la API.

La longitud y ausencia de reglas de composición toman como referencia
[NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b.html). Esto no declara
conformidad integral con esa norma: quedan por revisar corpus, almacenamiento,
Unicode, MFA y los demás controles de producción.

Una cuenta anterior puede iniciar sesión y confirmar una operación con su clave
actual de hasta 128 unidades, aunque sea más corta. El mínimo nuevo se exige
cuando elige una clave nueva. Un rechazo no consume el enlace de recuperación,
revoca accesos, cambia el hash o crea un evento de cambio de contraseña.
Una clave rechazada por la lista devuelve 400 y código cerrado `password_policy`;
la interfaz muestra un mensaje fijo, nunca el contenido libre de la respuesta.

## Límite de sesiones por cuenta

Por defecto se admiten diez sesiones web no revocadas y no vencidas por cuenta.
La lista de **Mi cuenta → Ver sesiones activas** muestra el límite configurado
por la API mediante `maxActiveSessions`. La configuración es
`AuthSecurity:MaxActiveSessions` (variable de proceso
`AuthSecurity__MaxActiveSessions`), entero de 1 a 100; cambiarla requiere reiniciar.
No modifica el límite independiente de dispositivos.

Solo después de verificar la contraseña correcta se consulta capacidad. Al estar
completa, login devuelve 409 sin emitir tokens, revocar accesos o registrar una
sesión nueva. Contraseña incorrecta sigue devolviendo 401 y aplicando el bloqueo
habitual. La respuesta no se usa para revelar capacidad a alguien sin la clave.

Podés cerrar una sesión desde otro navegador o recuperar la contraseña para
cerrar todos los accesos. La renovación conserva su sesión y no ocupa otro lugar.
Las vencidas o revocadas no cuentan. Reducir el límite no cierra automáticamente
sesiones antiguas por encima del nuevo valor: bloquea nuevas aperturas hasta que
se libere capacidad. Tampoco expulsa automáticamente al navegador más antiguo.

El conteo y el alta coordinan la versión del usuario en la transacción de
`SaveChanges`. Un login concurrente que pierde la versión vuelve a leer y contar;
no basta con el contador de memoria de una instancia. PostgreSQL se prueba con
ocho logins concurrentes disputando dos lugares, en tres rondas. Esto no resuelve
los límites globales por IP, los proxies ni toda la operación distribuida.

Evidencia y omisiones de suites en el [changelog](../CHANGELOG.md).
