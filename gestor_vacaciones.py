# -*- coding: utf-8 -*-
import os
import sys
import json
import webview

class BridgeAPI:
    def __init__(self):
        self.filename = "datos_vacaciones.json"
        self.window = None

    def set_window(self, window):
        """
        Guarda la referencia a la ventana activa de pywebview.
        """
        self.window = window

    def guardar_archivo(self, nombre_sugerido, contenido, es_base64=False):
        """
        Muestra un diálogo nativo de guardar archivo y escribe el contenido.
        """
        try:
            if not self.window:
                print("Error: No hay referencia a la ventana activa.")
                return None

            # Determinar tipo de filtro de archivos por extensión
            ext = os.path.splitext(nombre_sugerido)[1].lower()
            file_types = ('Todos los archivos (*.*)',)
            if ext == '.json':
                file_types = ('Archivos JSON (*.json)',)
            elif ext == '.csv':
                file_types = ('Archivos CSV (*.csv)',)
            elif ext == '.pdf':
                file_types = ('Archivos PDF (*.pdf)',)
            elif ext == '.xls':
                file_types = ('Archivos de Excel (*.xls)',)

            # Abrir diálogo nativo
            file_path = self.window.create_file_dialog(
                webview.SAVE_DIALOG,
                directory=os.path.expanduser("~"),
                save_filename=nombre_sugerido,
                file_types=file_types
            )

            if not file_path:
                return None

            # pywebview a veces retorna una tupla o lista
            if isinstance(file_path, (list, tuple)):
                if len(file_path) > 0:
                    file_path = file_path[0]
                else:
                    return None

            # Guardar contenido
            if es_base64:
                import base64
                if ',' in contenido:
                    contenido = contenido.split(',', 1)[1]
                data = base64.b64decode(contenido)
                with open(file_path, "wb") as f:
                    f.write(data)
            else:
                with open(file_path, "w", encoding="utf-8") as f:
                    f.write(contenido)

            return file_path
        except Exception as e:
            print(f"Error al guardar archivo nativo: {e}")
            return None


    def cargar_datos_locales(self):
        """
        Carga el estado consolidado de las vacaciones.
        Si no existe el archivo consolidado, intenta migrar
        los datos de los archivos separados anteriores.
        """
        try:
            # 1. Si existe datos_vacaciones.json, cargarlo directamente
            if os.path.exists(self.filename):
                with open(self.filename, "r", encoding="utf-8") as f:
                    return f.read()

            # 2. Si no existe, intentar migrar desde trabajadores.json y festivos.json
            trabajadores = {}
            festivos = []
            titulo_pagina = "Planificación de Vacaciones"
            year = 2026
            migrado = False

            if os.path.exists("trabajadores.json"):
                try:
                    with open("trabajadores.json", "r", encoding="utf-8") as f:
                        data = json.load(f)
                        if isinstance(data, dict) and "trabajadores" in data:
                            titulo_pagina = data.get("titulo_pagina", titulo_pagina)
                            year = data.get("year", year)
                            festivos = data.get("festivos", [])
                            trabajadores = data.get("trabajadores", {})
                        elif isinstance(data, dict):
                            for nombre, info in data.items():
                                if isinstance(info, list):
                                    trabajadores[nombre] = {
                                        "vacaciones": info,
                                        "dias_base": 22,
                                        "dias_extras": 0
                                    }
                                else:
                                    trabajadores[nombre] = {
                                        "vacaciones": info.get("vacaciones", []),
                                        "dias_base": info.get("dias_base", 22),
                                        "dias_extras": info.get("dias_extras", 0)
                                    }
                        migrado = True
                except Exception as e:
                    print(f"Error al migrar trabajadores.json: {e}")

            if os.path.exists("festivos.json") and not festivos:
                try:
                    with open("festivos.json", "r", encoding="utf-8") as f:
                        festivos = json.load(f)
                        migrado = True
                except Exception as e:
                    print(f"Error al migrar festivos.json: {e}")

            datos_consolidados = {
                "titulo_pagina": titulo_pagina,
                "year": year,
                "festivos": festivos,
                "trabajadores": trabajadores
            }

            datos_str = json.dumps(datos_consolidados, indent=4, ensure_ascii=False)

            if migrado:
                with open(self.filename, "w", encoding="utf-8") as f:
                    f.write(datos_str)

            return datos_str

        except Exception as e:
            print(f"Error al cargar datos locales: {e}")
            return "{}"

    def guardar_datos_locales(self, datos_json):
        """
        Guarda los datos consolidados en formato JSON.
        """
        try:
            with open(self.filename, "w", encoding="utf-8") as f:
                f.write(datos_json)
            return True
        except Exception as e:
            print(f"Error al guardar datos locales: {e}")
            return False

def obtener_ruta_recurso(ruta_relativa):
    """
    Retorna la ruta absoluta del recurso, compatible tanto para
    desarrollo como para el ejecutable empaquetado con PyInstaller.
    """
    try:
        # PyInstaller crea una carpeta temporal y almacena su ruta en _MEIPASS
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, ruta_relativa)

def main():
    api = BridgeAPI()
    
    # Obtener la ruta del archivo index.html local
    html_path = obtener_ruta_recurso("index.html")
    
    if not os.path.exists(html_path):
        # Fallback para desarrollo si se ejecuta desde otro directorio
        html_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "index.html")

    # Iniciar la ventana nativa ligera mediante pywebview
    window = webview.create_window(
        title="Gestor de Vacaciones Pro",
        url=html_path,
        js_api=api,
        width=1280,
        height=800,
        maximized=True
    )
    api.set_window(window)
    
    webview.start(debug=False)

if __name__ == "__main__":
    main()