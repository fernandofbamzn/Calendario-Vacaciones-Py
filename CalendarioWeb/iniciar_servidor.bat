@echo off
echo =========================================================
echo Iniciando servidor local para Calendario Vacaciones
echo =========================================================
echo.
echo Para evitar errores de CORS al usar Babel, es necesario
echo servir los archivos a traves de HTTP en lugar de usar
echo el protocolo file://
echo.
echo Abriendo el navegador en http://localhost:8000...
start http://localhost:8000
echo.
echo Presiona Ctrl+C para detener el servidor.
python -m http.server 8000
