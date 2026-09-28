# Convivencia con SEB (0.0.182)

Identidad MSI, Program Files/ElRinconSeguro, servicio ElRinconSeguro, canales
net.pipe, mutex, AppData y ProgramData independientes. Descargas predeterminadas:
Descargas/ElRinconSeguro; las rutas configuradas expresamente tienen prioridad.
Protocolos rincon/rincons y extensión .ers propios; seb/sebs/.seb quedan para SEB.

Migración: retira dentro de la transacción MSI las versiones 0.0.1 a 0.0.181 de
idioma 1034 con la identidad histórica. Las actualizaciones siguientes usan la
nueva identidad. Los datos del perfil compartido anterior no se copian ni borran,
pues pueden pertenecer a SEB; los documentos anteriores siguen en su ubicación.

Si también figura instalado SEB original, el MSI bloquea la migración antigua:
el desinstalador antiguo comparte componentes y elimina el servicio original.
Retirar primero El Rincón Seguro antiguo, reparar SEB e instalar este MSI.
Las instalaciones independientes nuevas pueden convivir en cualquier orden.
Esto no significa ejecutar dos exámenes bloqueados simultáneamente.

En el primer salto, el actualizador antiguo intenta reabrir la ruta anterior.
Abrir el cliente desde el nuevo acceso directo; las siguientes actualizaciones
ya utilizan la ruta nueva. No se deja un ejecutable puente en la carpeta de SEB.

Validación sin instalar: build_and_sign.ps1, verify_installer.ps1 y pruebas de
configuración. La prueba real de actualización debe hacerse en otra máquina
Windows: SEB solo, Rincón antiguo solo, ambos antiguos y dos versiones nuevas.
Comprobar servicio, asociaciones, archivos conservados, rollback, ausencia de
productos duplicados y reinicios. La inspección MSI no sustituye esta prueba.

Resultado de validación 28-09-2026: MSI y cuatro ejecutables principales con firma
válida; 54 archivos versionados a 0.0.182; secuencia MSI 1500 < 1501 < 4000;
REBOOT=ReallySuppress. verify_updater.ps1 valida la reapertura y analiza el script
sin ejecutarlo. Pruebas de configuración: 69/72 con cultura española, incluidas
las nuevas comprobaciones de aislamiento; los tres tests de serialización que
usan Convert.ToDouble sin cultura pasan al repetirlos con en-US.
Pendiente: prueba real de migración en un Windows separado, antes de publicar.

## Corrección del actualizador 0.0.183

Se observaron procesos de inicio antiguos activos y sus auxiliares detenidos en
Wait-Process, sin que se hubiera lanzado Windows Installer. El inicio ahora devuelve
el control a Main y libera el mutex antes de terminar. El auxiliar espera 15 segundos
y, si es necesario, cierra exclusivamente el PID solicitante después de comprobar
su hora de inicio y ejecutable. No busca ni cierra procesos por nombre.

El MSI se ejecuta con registro detallado, sin reinicios. Se comprueba su código de
salida, la existencia del ejecutable y la coincidencia con la versión anunciada
antes de reabrir. Los errores se muestran y conservan la descarga para diagnóstico.
El registro MSI está junto al registro del actualizador, con sufijo .msi.log.

Las versiones anteriores que ya se bloquean antes de instalar requieren ejecutar
manualmente el nuevo MSI una vez. No pueden recibir la corrección de su propio
actualizador hasta completar esa instalación. Cancelar primero sus auxiliares
pendientes; después cerrar los procesos de inicio antiguos. No cerrar un examen.

Pruebas sin instalar: test_updater.ps1 ejecuta el flujo real con procesos y MSI
simulados en nueve escenarios: salida normal, bloqueo, PID reutilizado, ejecutable
inesperado, error MSI, cancelación UAC, ejecutable ausente, versión equivocada y
código 3010 sin reinicio. verify_updater.ps1 analiza el script incrustado compilado.
