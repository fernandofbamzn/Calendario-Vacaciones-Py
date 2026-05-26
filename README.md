# Gestor de Vacaciones Pro — CalendarioWPF

Aplicación de escritorio para **planificación y gestión de vacaciones de equipos**, construida con WPF/.NET 10. Permite registrar festivos oficiales, asignar vacaciones a trabajadores con seguimiento de cupos anuales, y exportar la planificación a PDF, Excel y CSV.

---

## 📋 Tabla de Contenidos

- [Tecnologías](#tecnologías)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Arquitectura](#arquitectura)
- [Cómo Compilar](#cómo-compilar)
- [Persistencia de Datos](#persistencia-de-datos)
- [Convenciones de Desarrollo](#convenciones-de-desarrollo)

---

## Tecnologías

| Componente       | Tecnología                          |
|-----------------|-------------------------------------|
| UI              | WPF (Windows Presentation Foundation) |
| Framework       | .NET 10, C# 12                      |
| Exportación PDF | PDFSharp 6.x                        |
| Exportación Excel | ClosedXML                          |
| Serialización   | System.Text.Json                    |
| Build           | dotnet CLI / MSBuild                |

---

## Estructura del Proyecto

```
CalendarioWPF/
├── Models/                         # POCOs de datos (sin lógica)
│   ├── PlanVacaciones.cs           # Modelo raíz (trabajadores, festivos, año cupo)
│   ├── InfoTrabajador.cs           # Días de vacaciones, cupo y imputaciones
│   ├── ImportResult.cs             # Resultado de operaciones de importación
│   └── AppConfig.cs                # Configuración de presentación y exportación
│
├── Services/                       # Lógica de negocio y acceso a datos
│   ├── IDataManager.cs             # ★ Interfaz documentada del gestor de persistencia
│   ├── DataManager.cs              # Carga/guardado JSON y exportaciones JSON/CSV/Gantt
│   ├── IAppConfigManager.cs        # ★ Interfaz del gestor de configuración
│   ├── AppConfig.cs                # Implementación (AppConfigManager)
│   ├── IRangoVacacionesHelper.cs   # ★ Interfaz del helper de rangos
│   ├── RangoVacacionesHelper.cs    # Agrupación textual y cómputo de días consumidos
│   ├── IPdfExportService.cs        # ★ Interfaz del servicio de exportación PDF
│   ├── PdfExportService.cs         # Generación de PDFs mensuales y Gantt
│   ├── IExcelExportService.cs      # ★ Interfaz del servicio de exportación Excel
│   └── ExcelExportService.cs       # Generación de libros Excel con ClosedXML
│
├── Views/                          # Clases parciales de la ventana principal
│   ├── MainWindow.Panel.cs         # Toolbar de trabajador, panel de cupo, modos de edición
│   ├── MainWindow.Calendario.cs    # Calendario mensual, drag-to-select, panel textual
│   ├── MainWindow.Gantt.cs         # Tabla Gantt interactiva
│   └── MainWindow.Exports.cs       # Importación, exportación y visor de logs
│
├── Dialogs/                        # Diálogos modales secundarios
│   ├── LimpiarDialog.xaml          # Diálogo de limpieza selectiva de datos
│   └── LimpiarDialog.xaml.cs
│
├── MainWindow.xaml                 # Layout XAML principal
├── MainWindow.xaml.cs              # Clase parcial base: estado, constructor, persistencia
├── ConfigurationWindow.xaml        # Ventana de configuración (trabajadores, festivos, cupo)
├── ConfigurationWindow.xaml.cs
├── App.xaml                        # Recursos de estilos y colores globales
├── AssemblyInfo.cs
│
├── VacacionesData.cs               # Global using → Models/ (compatibilidad)
├── RangoVacacionesHelper.cs        # Global using → Services/ (compatibilidad)
│
└── CalendarioWPF.csproj
```

> **★** = Leer la interfaz es suficiente para entender el contrato del componente sin necesidad de revisar la implementación.

---

## Arquitectura

### Conceptos Clave

**Año de Cupo (`_datos.Year`)**: El año al que se *imputan* las vacaciones. Es independiente del año natural en que se disfrutan. Por ejemplo, vacaciones del cupo 2026 pueden disfrutarse en enero de 2027.

**Año Visualizado (`_visualizedYear`)**: El año del calendario que se muestra en la pantalla. No afecta a las imputaciones, solo a la vista.

**Imputaciones (`InfoTrabajador.Imputaciones`)**: Diccionario `fecha → año_cupo` que registra a qué cupo pertenece cada vacación. Si una fecha no aparece en este diccionario, se asume el año natural de la fecha.

### Flujo de Datos

```
[MainWindow] ──cargar/guardar──► [DataManager] ──► datos_vacaciones.json
     │
     ├── [RangoVacacionesHelper] (solo lectura, sin estado)
     │
     ├── [PdfExportService]  ──► .pdf
     ├── [ExcelExportService] ──► .xlsx
     └── [AppConfigManager]  ──► app_config.json
```

### Convención de Color en la UI

| Color    | Significado                                              |
|----------|----------------------------------------------------------|
| Azul     | Vacaciones del cupo activo (año de cupo = `_datos.Year`) |
| Lavanda  | Vacaciones de otro cupo diferente al activo              |
| Indigo   | Día con vacaciones de varios trabajadores (mezcla de cupos) |
| Rojo     | Festivo oficial                                          |
| Gris     | Fin de semana                                            |

---

## Cómo Compilar

### Requisitos

- .NET 10 SDK ([descargar](https://dotnet.microsoft.com/download/dotnet/10.0))
- Windows 10/11 (WPF solo funciona en Windows)

### Compilación en modo Debug

```powershell
cd "c:\ReposGit\Calendario Vacaciones Py\CalendarioWPF"
dotnet build
```

### Ejecución

```powershell
dotnet run
```

O abrir `CalendarioWPF.slnx` con Visual Studio 2022+ y ejecutar con F5.

---

## Persistencia de Datos

Los datos se guardan en el directorio de trabajo de la aplicación:

| Archivo              | Contenido                                       |
|---------------------|-------------------------------------------------|
| `datos_vacaciones.json` | Plan completo: trabajadores, festivos, año, vacaciones con imputaciones |
| `app_config.json`   | Configuración de presentación (meses, orientación PDF, etc.) |

### Formato JSON de Vacaciones

Cada fecha de vacaciones se almacena como una cadena `"dd/MM/yyyy"` dentro de `vacaciones[]`.
Las imputaciones se guardan en `imputaciones{}` como un objeto `"fecha": año_cupo`.

```json
{
  "trabajadores": {
    "Juan García": {
      "vacaciones": ["01/08/2026", "15/01/2027"],
      "dias_base": 22,
      "dias_extras": 0,
      "imputaciones": {
        "01/08/2026": 2026,
        "15/01/2027": 2026
      }
    }
  }
}
```

> **Nota:** El campo `anos_a_exportar` (sin ñ) reemplaza al antiguo `años_a_exportar`. Los archivos antiguos se leen correctamente gracias a la propiedad fallback en `AppConfig`.

---

## Convenciones de Desarrollo

### Idioma

- **Código** (variables, métodos, propiedades): inglés, según convención C#.
- **Comentarios, XML docs, commits y documentación**: castellano (español de España).
- **Fechas**: formato `DD/MM/YYYY`.

### Regla Token-Saving (Interfaces)

Cada servicio o componente lógico significativo **debe tener su propia interfaz** en `Services/I*.cs` con documentación XML completa en castellano. Esto permite a los agentes de IA entender el contrato del componente leyendo únicamente la interfaz, sin necesidad de procesar la implementación completa.

### Principio de Cambios Quirúrgicos

Solo se modifica el código estrictamente necesario. No se refactoriza código adyacente que funcione correctamente. Se mantiene el estilo de codificación preexistente.

### Clases Parciales de Vista

La lógica de la ventana principal está dividida en clases parciales dentro de `Views/`. Cada fichero tiene una responsabilidad única:
- `MainWindow.Panel.cs` → Gestión de trabajador y cupo
- `MainWindow.Calendario.cs` → Renderizado del calendario y drag-to-select
- `MainWindow.Gantt.cs` → Tabla Gantt
- `MainWindow.Exports.cs` → Importación y exportación
