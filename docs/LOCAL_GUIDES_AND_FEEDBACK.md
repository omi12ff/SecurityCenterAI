# Guías locales y feedback de análisis

Implementado el 22 de octubre de 2026. La elección para este bloque fue usar
guías locales sin servicio de IA externo. No requiere instalar herramientas ni
configurar credenciales adicionales.

El 2026-10-25 el usuario autorizó añadir una [explicación opcional de IA por
análisis](AI_EXPLANATIONS.md), desactivada por defecto y solicitada con botón
después de revisar el aviso de datos. Este cambio conserva las guías locales y su disponibilidad
ante fallos; la valoración descrita aquí corresponde a la guía local. Al revisar
el estado el 2026-10-26, el usuario habilitó la configuración del producto con
su clave y la ruta explícita `openrouter/openrouter/free`. El adaptador devolvió
resumen y diez explicaciones válidas en una prueba real con estados ficticios;
no se utilizó el informe personal. Se muestran razones de fallo y la guía local
permanece disponible. El usuario pidió retirar la casilla adicional.

## Cómo usarlo

1. Entrá a **Dispositivos → Ver equipo e historial**.
2. En un análisis guardado, elegí **Entender este análisis**.
3. Consultá la interpretación, evidencia, revisión manual y límites de cada
   comprobación. Las referencias abren documentación de Microsoft.
4. Al final, elegí **Me ayudó**, **No quedó clara** o **Encontré un error**.
   Podés agregar un comentario de hasta 1000 caracteres y guardar la valoración.
   Un envío posterior actualiza tu valoración anterior.

Funciona sobre los análisis existentes; no necesitás generar otro código ni
enviar un nuevo informe. Si querés comprobar cambios en Windows, ejecutá un nuevo
análisis y envialo explícitamente con el agente como indica su guía.

## Interpretación y límites

La guía versión 1 interpreta los cuatro estados de las diez reglas versión 1
del agente Windows. No cambia pesos, puntajes, cobertura ni evidencia guardada.
Un estado desconocido conserva su incertidumbre; no demuestra que una protección
esté desactivada. Un resultado correcto es una observación puntual autodeclarada.
Versiones o fuentes incompatibles reciben una explicación de alcance limitado,
sin inventar un diagnóstico.

Las plantillas y enlaces se seleccionan por regla, versión y estado reconocidos;
la evidencia del cliente se muestra como texto y no se interpreta como instrucciones.
Las acciones proponen consultas manuales, sin ejecutar comandos ni modificar Windows.
Las guías describen las consultas limitadas existentes: Windows Update no confirma
parches instalados, SMB1 solo observa el servidor, UAC solo lee EnableLUA y Defender
no evalúa otros antivirus.

Referencias oficiales consultadas para preparar las plantillas:

- [Protección contra virus y amenazas](https://support.microsoft.com/en-us/windows/security/threat-malware-protection/virus-and-threat-protection-in-the-windows-security-app.).
- [Firewall y perfiles de red](https://support.microsoft.com/en-us/windows/security/windows-security/firewall-and-network-protection-in-the-windows-security-app).
- [Seguridad del dispositivo, TPM y arranque seguro](https://support.microsoft.com/en-us/windows/security/windows-security/device-security-in-the-windows-security-app).
- [BitLocker](https://support.microsoft.com/en-us/windows/security/encryption/bitlocker-overview).
- [Funcionamiento de Windows Update](https://learn.microsoft.com/en-us/windows/deployment/update/how-windows-update-works).
- [Control de cuentas de usuario](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/).
- [Componentes SMB](https://learn.microsoft.com/en-us/windows-server/storage/file-server/troubleshoot/detect-enable-and-disable-smbv1-v2-v3).

## API y almacenamiento

Los dos endpoints requieren una sesión Bearer de cuenta y verifican conjuntamente
el propietario, dispositivo y análisis. Una credencial Device no permite usarlos;
otro propietario recibe 404. La revocación del dispositivo conserva el historial.

- `GET /api/devices/{deviceId}/analyses/{analysisId}/explanation`: devuelve el
  análisis original, guía local actual, versión y valoración guardada del propietario.
- `POST /api/devices/{deviceId}/analyses/{analysisId}/feedback`: recibe
  `rating` (`helpful`, `unclear` o `incorrect`) y `comment` opcional. Rechaza campos
  adicionales, controles no permitidos y cuerpos mayores a 8 KiB. Comparte el
  límite `DeviceAccount` de 60 solicitudes/minuto/IP y el límite global.

La migración `20261022123515_AnalysisExplanationFeedback` agrega `analysis_feedback`
con columnas en español: identificadores, valoración, comentario, versión de guía
y fechas UTC del servidor. El índice único usuario/análisis evita duplicados;
los primeros envíos concurrentes se reintentan y la última escritura prevalece.
Se guarda una valoración por análisis y propietario, sin publicar comentarios
ni ofrecer una lista de feedback de otras cuentas.

Privado significa accesible mediante la aplicación solo desde la cuenta dueña;
quien administra la base local y sus copias puede leerlo. No incluyas secretos.
El panel **Control de este análisis** permite exportar el resultado con su
feedback propio o eliminarlo con confirmación. El borrado elimina todo el feedback
del análisis; una marca mínima impide reenvíos del mismo reporte. Copias descargadas,
informes locales y backups permanecen. No hay retención automática de contenido;
el [borrado de cuenta](ACCOUNT_DELETION.md) elimina su feedback de la base activa.
Logout, revocación y reset no borran feedback.
[Uso y límites](ANALYSIS_HISTORY.md). No se guarda el historial de ediciones.
La versión almacenada identifica la plantilla valorada, no una evaluación de IA.

## Prueba privada local

Este bloque prepara el recorrido y su feedback para pruebas locales supervisadas.
No publica la aplicación ni inicia una beta externa. Para una sesión de prueba:

1. Usar una cuenta y dispositivo de prueba con autorización de su dueño.
2. Verificar vínculo, envío explícito, resultado parcial y guía por comprobación.
3. Registrar si la explicación fue clara y qué dato faltó para decidir una acción.
4. Actualizar la valoración y comprobar que persiste al recargar.
5. Reportar defectos sin contraseñas, códigos, tokens ni claves de recuperación.

Antes de invitar usuarios externos siguen pendientes distribución/compatibilidad
del agente, operación de cuentas/correo, retención, privacidad, alojamiento y los
controles de producción de [SECURITY.md](../SECURITY.md). La explicación opcional
de IA tiene su [contrato de datos y límites](AI_EXPLANATIONS.md); falta configurar
y validar un proveedor y revisar sus condiciones antes de habilitar consultas.
