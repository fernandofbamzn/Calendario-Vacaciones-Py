# Calendario de Vacaciones (Versión Python)

Esta carpeta contiene la versión original del Calendario de Vacaciones desarrollada en Python utilizando `pywebview` para renderizar la interfaz web.

## Requisitos

- Python 3.8 o superior
- Las dependencias listadas (se pueden instalar desde un entorno virtual)

## Instalación y Ejecución Local

1. Activar el entorno virtual:
   ```cmd
   .\env\Scripts\activate
   ```
2. Instalar dependencias si no están instaladas:
   ```cmd
   pip install pywebview pyinstaller
   ```
3. Ejecutar la aplicación:
   ```cmd
   python gestor_vacaciones.py
   ```

## Generar el Ejecutable

Para crear un ejecutable `.exe` independiente que contenga todos los archivos (HTML, JS, CSS, etc.) embebidos de manera que funcione en cualquier PC sin requerir Python:

1. Asegúrate de estar dentro del entorno virtual y en el directorio `CalendarioPy`.
2. Ejecuta PyInstaller con el archivo `.spec` que ya está configurado para empaquetar los recursos web:
   ```cmd
   pyinstaller gestor_vacaciones.spec --clean
   ```

3. El ejecutable final se generará dentro de la carpeta `dist/`. Este archivo es el que se puede distribuir.

## Notas

- El archivo `gestor_vacaciones.spec` ya se encarga de incluir `index.html`, `app.js`, `style.css` y las librerías de `jspdf`.
- Si se modifican los nombres de los archivos web, se debe actualizar la sección `datas` en `gestor_vacaciones.spec`.
