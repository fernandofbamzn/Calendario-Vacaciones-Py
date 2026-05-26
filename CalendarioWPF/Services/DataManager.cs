using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Estructura que almacena el resultado de una operación de importación.
    /// </summary>
    public class ImportResult
    {
        /// <summary>
        /// El tipo de datos importados (ej. "Consolidado Completo", "Festivos Oficiales (CSV)").
        /// </summary>
        public string Tipo { get; set; } = "";

        /// <summary>
        /// Mensaje descriptivo con el resultado de la importación.
        /// </summary>
        public string Msg { get; set; } = "";

        /// <summary>
        /// Instancia del plan de vacaciones con los datos actualizados tras la importación.
        /// </summary>
        public PlanVacaciones DatosActualizados { get; set; } = null!;
    }

    /// <summary>
    /// Servicio encargado de la gestión de persistencia local, parseo e importación/exportación de formatos planos (JSON y CSV).
    /// </summary>
    public class DataManager : IDataManager
    {
        /// <summary>
        /// Instancia única (Singleton) para acceso dinámico a través de la interfaz IDataManager.
        /// </summary>
        public static IDataManager Instance { get; } = new DataManager();

        /// <summary>
        /// Logger utilizado por el DataManager para registrar errores de persistencia.
        /// Se puede reemplazar con una implementación distinta en el arranque de la aplicación.
        /// Por defecto apunta al singleton global <see cref="AppLogger.Instance"/>.
        /// </summary>
        public static IAppLogger Logger { get; set; } = AppLogger.Instance;

        private const string DatosFilename = "datos_vacaciones.json";

        /// <summary>
        /// Carga los datos desde el archivo JSON local. Si no existe, inicializa un plan vacío.
        /// </summary>
        /// <returns>La instancia de PlanVacaciones cargada o inicializada.</returns>
        public static PlanVacaciones CargarDatos()
        {
            if (File.Exists(DatosFilename))
            {
                string json = File.ReadAllText(DatosFilename, Encoding.UTF8);
                return JsonSerializer.Deserialize<PlanVacaciones>(json) ?? InicializarDatosVacios();
            }
            return InicializarDatosVacios();
        }

        /// <summary>
        /// Inicializa un plan de vacaciones vacío para el año actual o por defecto (2026).
        /// </summary>
        /// <returns>Un objeto PlanVacaciones nuevo.</returns>
        public static PlanVacaciones InicializarDatosVacios()
        {
            return new PlanVacaciones
            {
                TituloPagina = "Planificación de Vacaciones",
                Year = DateTime.Today.Year,
                Festivos = new List<string>(),
                Trabajadores = new Dictionary<string, InfoTrabajador>()
            };
        }

        /// <summary>
        /// Guarda el estado del plan de vacaciones en el archivo JSON local de manera síncrona.
        /// Si ocurre un error de E/S (disco lleno, permisos, etc.) lanza una excepción para que
        /// la capa de presentación pueda mostrársela al usuario.
        /// </summary>
        /// <param name="datos">Los datos a guardar.</param>
        /// <exception cref="IOException">Si no se puede escribir en el archivo de datos.</exception>
        public static void GuardarDatos(PlanVacaciones datos)
        {
            try
            {
                string json = JsonSerializer.Serialize(datos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DatosFilename, json, Encoding.UTF8);
                Logger.Info($"Plan guardado correctamente en '{DatosFilename}'.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error al guardar '{DatosFilename}'", ex);
                // Relanzar para que MainWindow pueda mostrarlo en la barra de estado
                throw;
            }
        }

        /// <summary>
        /// Procesa un bloque de texto en formato JSON o CSV e importa los datos en la configuración actual.
        /// </summary>
        /// <param name="datosActuales">El estado actual del planizador de vacaciones.</param>
        /// <param name="text">El contenido de texto a importar.</param>
        /// <param name="esJson">Especifica si el texto es JSON (true) o CSV (false).</param>
        /// <returns>El resultado de la importación conteniendo los datos actualizados.</returns>
        public static ImportResult ImportarDesdeTexto(PlanVacaciones datosActuales, string text, bool esJson)
        {
            var datos = datosActuales ?? InicializarDatosVacios();

            if (esJson)
            {
                using (var doc = JsonDocument.Parse(text))
                {
                    var root = doc.RootElement;

                    // 1. Detectar si es el JSON Consolidado Completo
                    if (root.ValueKind == JsonValueKind.Object && (root.TryGetProperty("trabajadores", out _) || root.TryGetProperty("festivos", out _) || root.TryGetProperty("titulo_pagina", out _)))
                    {
                        if (root.TryGetProperty("titulo_pagina", out var titleProp))
                        {
                            datos.TituloPagina = titleProp.GetString() ?? "Planificación de Vacaciones";
                        }
                        if (root.TryGetProperty("year", out var yearProp))
                        {
                            datos.Year = yearProp.GetInt32();
                        }
                        if (root.TryGetProperty("festivos", out var festivosProp))
                        {
                            datos.Festivos = JsonSerializer.Deserialize<List<string>>(festivosProp.GetRawText()) ?? new List<string>();
                        }
                        if (root.TryGetProperty("trabajadores", out var trabsProp))
                        {
                            datos.Trabajadores = JsonSerializer.Deserialize<Dictionary<string, InfoTrabajador>>(trabsProp.GetRawText()) ?? new Dictionary<string, InfoTrabajador>();
                        }

                        return new ImportResult 
                        { 
                            Tipo = "Consolidado Completo", 
                            Msg = "Se ha importado el estado completo del planificador de vacaciones.",
                            DatosActualizados = datos
                        };
                    }

                    // 2. Detectar si es una lista simple de Festivos
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        int count = 0;
                        var list = JsonSerializer.Deserialize<List<string>>(root.GetRawText()) ?? new List<string>();
                        foreach (var item in list)
                        {
                            if (Regex.IsMatch(item, @"^\d{2}/\d{2}/\d{4}$"))
                            {
                                if (!datos.Festivos.Contains(item))
                                {
                                    datos.Festivos.Add(item);
                                    count++;
                                    // Limpiar de las vacaciones de cualquier trabajador si se vuelve festivo
                                    foreach (var w in datos.Trabajadores.Values)
                                    {
                                        w.Vacaciones.Remove(item);
                                        w.Imputaciones?.Remove(item);
                                    }
                                }
                            }
                        }
                        return new ImportResult 
                        { 
                            Tipo = "Festivos Oficiales (JSON)", 
                            Msg = $"Se han importado {count} festivos oficiales nuevos.",
                            DatosActualizados = datos
                        };
                    }

                    // 3. Detectar si es Configuración de Trabajadores (nombre -> info) o Vacaciones (nombre -> lista)
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        bool esConfigTrabajadores = false;
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Object && (prop.Value.TryGetProperty("dias_base", out _) || prop.Value.TryGetProperty("dias_extras", out _)))
                            {
                                esConfigTrabajadores = true;
                                break;
                            }
                        }

                        if (esConfigTrabajadores)
                        {
                            int count = 0;
                            var dict = JsonSerializer.Deserialize<Dictionary<string, InfoTrabajador>>(root.GetRawText()) ?? new Dictionary<string, InfoTrabajador>();
                            foreach (var kvp in dict)
                            {
                                string nombre = kvp.Key;
                                if (string.IsNullOrEmpty(nombre) || nombre.ToUpper() == "FESTIVO") continue;

                                if (datos.Trabajadores.ContainsKey(nombre))
                                {
                                    datos.Trabajadores[nombre].DiasBase = kvp.Value.DiasBase;
                                    datos.Trabajadores[nombre].DiasExtras = kvp.Value.DiasExtras;
                                }
                                else
                                {
                                    datos.Trabajadores[nombre] = new InfoTrabajador
                                    {
                                        Vacaciones = new List<string>(),
                                        Imputaciones = new Dictionary<string, int>(),
                                        DiasBase = kvp.Value.DiasBase,
                                        DiasExtras = kvp.Value.DiasExtras
                                    };
                                }
                                count++;
                            }
                            return new ImportResult 
                            { 
                                Tipo = "Configuración de Personal (JSON)", 
                                Msg = $"Se han importado/actualizado {count} perfiles de trabajadores.",
                                DatosActualizados = datos
                            };
                        }
                        else
                        {
                            // Vacaciones asignadas (objeto de listas de elementos dinámicos)
                            int countW = 0;
                            var dictVacs = JsonSerializer.Deserialize<Dictionary<string, List<JsonElement>>>(root.GetRawText()) ?? new Dictionary<string, List<JsonElement>>();
                            foreach (var kvp in dictVacs)
                            {
                                string nombre = kvp.Key;
                                if (string.IsNullOrEmpty(nombre) || nombre.ToUpper() == "FESTIVO") continue;

                                var validDates = new List<string>();
                                var imputaciones = new Dictionary<string, int>();

                                foreach (var element in kvp.Value)
                                {
                                    if (element.ValueKind == JsonValueKind.String)
                                    {
                                        // Formato antiguo: cadena simple "dd/MM/yyyy"
                                        string f = element.GetString() ?? "";
                                        if (Regex.IsMatch(f, @"^\d{2}/\d{2}/\d{4}$") && !datos.Festivos.Contains(f))
                                        {
                                            validDates.Add(f);
                                            int qYear = int.Parse(f.Substring(6, 4));
                                            imputaciones[f] = qYear;
                                        }
                                    }
                                    else if (element.ValueKind == JsonValueKind.Object)
                                    {
                                        // Formato nuevo: objeto { fecha, ano_cupo }
                                        if (element.TryGetProperty("fecha", out var dateProp))
                                        {
                                            JsonElement yearProp;
                                            bool hasYear = element.TryGetProperty("ano_cupo", out yearProp) || element.TryGetProperty("año_cupo", out yearProp);
                                            if (hasYear)
                                        {
                                            string f = dateProp.GetString() ?? "";
                                            int qYear = yearProp.GetInt32();
                                            if (Regex.IsMatch(f, @"^\d{2}/\d{2}/\d{4}$") && !datos.Festivos.Contains(f))
                                            {
                                                validDates.Add(f);
                                                imputaciones[f] = qYear;
                                            }
                                        }
                                    }
                                }
                            }

                                if (!datos.Trabajadores.ContainsKey(nombre))
                                {
                                    datos.Trabajadores[nombre] = new InfoTrabajador { Vacaciones = new List<string>(), Imputaciones = new Dictionary<string, int>(), DiasBase = 22, DiasExtras = 0 };
                                }
                                
                                datos.Trabajadores[nombre].Vacaciones = validDates;
                                if (datos.Trabajadores[nombre].Imputaciones == null)
                                {
                                    datos.Trabajadores[nombre].Imputaciones = new Dictionary<string, int>();
                                }
                                foreach (var impKvp in imputaciones)
                                {
                                    datos.Trabajadores[nombre].Imputaciones[impKvp.Key] = impKvp.Value;
                                }
                                countW++;
                            }
                            return new ImportResult 
                            { 
                                Tipo = "Vacaciones Asignadas (JSON)", 
                                Msg = $"Se han importado las vacaciones para {countW} trabajadores.",
                                DatosActualizados = datos
                            };
                        }
                    }
                }
                throw new Exception("Estructura JSON no reconocida o inválida.");
            }
            else
            {
                // Parser CSV Inteligente
                var filas = ParseCsv(text);
                if (filas.Count == 0) throw new Exception("Archivo CSV vacío.");

                var primeraFila = filas[0];

                // 1. Detectar si son Festivos (una fecha por fila en formato DD/MM/YYYY)
                bool esFestivo = true;
                foreach (var row in filas)
                {
                    if (row.Count != 1 || !Regex.IsMatch(row[0], @"^\d{2}/\d{2}/\d{4}$"))
                    {
                        esFestivo = false;
                        break;
                    }
                }

                if (esFestivo)
                {
                    int count = 0;
                    foreach (var row in filas)
                    {
                        string dateStr = row[0];
                        if (!datos.Festivos.Contains(dateStr))
                        {
                            datos.Festivos.Add(dateStr);
                            count++;
                            foreach (var w in datos.Trabajadores.Values)
                            {
                                w.Vacaciones.Remove(dateStr);
                                w.Imputaciones?.Remove(dateStr);
                            }
                        }
                    }
                    return new ImportResult 
                    { 
                        Tipo = "Festivos Oficiales (CSV)", 
                        Msg = $"Se han importado {count} festivos oficiales.",
                        DatosActualizados = datos
                    };
                }

                // 2. Distinguir entre Configuración de Trabajadores (Nombre, dias_base, dias_extras)
                // y Vacaciones Asignadas (Nombre, fecha1, fecha2, ...)
                bool esConfig = false;
                if (primeraFila.Count >= 2)
                {
                    string segVal = primeraFila[1];
                    if (int.TryParse(segVal, out _))
                    {
                        esConfig = true;
                    }
                }

                if (esConfig)
                {
                    int count = 0;
                    foreach (var row in filas)
                    {
                        string nombre = row[0];
                        if (string.IsNullOrEmpty(nombre) || nombre.ToUpper() == "FESTIVO") continue;

                        int dBase = (row.Count > 1 && int.TryParse(row[1], out int db)) ? db : 22;
                        int dExtras = (row.Count > 2 && int.TryParse(row[2], out int de)) ? de : 0;

                        if (datos.Trabajadores.ContainsKey(nombre))
                        {
                            datos.Trabajadores[nombre].DiasBase = dBase;
                            datos.Trabajadores[nombre].DiasExtras = dExtras;
                        }
                        else
                        {
                            datos.Trabajadores[nombre] = new InfoTrabajador
                            {
                                Vacaciones = new List<string>(),
                                Imputaciones = new Dictionary<string, int>(),
                                DiasBase = dBase,
                                DiasExtras = dExtras
                            };
                        }
                        count++;
                    }
                    return new ImportResult 
                    { 
                        Tipo = "Configuración de Personal (CSV)", 
                        Msg = $"Se han importado {count} perfiles de trabajadores.",
                        DatosActualizados = datos
                    };
                }
                else
                {
                    // Vacaciones asignadas en formato CSV
                    int count = 0;
                    foreach (var row in filas)
                    {
                        string nombre = row[0];
                        if (string.IsNullOrEmpty(nombre) || nombre.ToUpper() == "FESTIVO") continue;

                        var validDates = new List<string>();
                        var imputaciones = new Dictionary<string, int>();
                        for (int i = 1; i < row.Count; i++)
                        {
                            string cellVal = row[i];
                            if (string.IsNullOrEmpty(cellVal)) continue;

                            string dateStr = cellVal;
                            int qYear = 0;

                            if (cellVal.Contains(":"))
                            {
                                var parts = cellVal.Split(':');
                                dateStr = parts[0];
                                int.TryParse(parts[1], out qYear);
                            }
                            else
                            {
                                if (dateStr.Length >= 10)
                                {
                                    int.TryParse(dateStr.Substring(6, 4), out qYear);
                                }
                            }

                            if (Regex.IsMatch(dateStr, @"^\d{2}/\d{2}/\d{4}$") && !datos.Festivos.Contains(dateStr))
                            {
                                validDates.Add(dateStr);
                                if (qYear > 0)
                                {
                                    imputaciones[dateStr] = qYear;
                                }
                            }
                        }

                        if (!datos.Trabajadores.ContainsKey(nombre))
                        {
                            datos.Trabajadores[nombre] = new InfoTrabajador { Vacaciones = new List<string>(), Imputaciones = new Dictionary<string, int>(), DiasBase = 22, DiasExtras = 0 };
                        }
                        
                        datos.Trabajadores[nombre].Vacaciones = validDates;
                        if (datos.Trabajadores[nombre].Imputaciones == null)
                        {
                            datos.Trabajadores[nombre].Imputaciones = new Dictionary<string, int>();
                        }
                        foreach (var impKvp in imputaciones)
                        {
                            datos.Trabajadores[nombre].Imputaciones[impKvp.Key] = impKvp.Value;
                        }
                        count++;
                    }
                    return new ImportResult 
                    { 
                        Tipo = "Vacaciones Asignadas (CSV)", 
                        Msg = $"Se han cargado las vacaciones para {count} trabajadores.",
                        DatosActualizados = datos
                    };
                }
            }
        }

        /// <summary>
        /// Genera el string JSON correspondiente a la configuración de trabajadores.
        /// </summary>
        public static string ExportarTrabajadoresJson(PlanVacaciones datos)
        {
            var exportObj = datos.Trabajadores.ToDictionary(
                kvp => kvp.Key,
                kvp => new { dias_base = kvp.Value.DiasBase, dias_extras = kvp.Value.DiasExtras }
            );
            return JsonSerializer.Serialize(exportObj, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Genera el contenido CSV correspondiente a la configuración de trabajadores.
        /// </summary>
        public static string ExportarTrabajadoresCsv(PlanVacaciones datos)
        {
            var sb = new StringBuilder();
            foreach (var kvp in datos.Trabajadores)
            {
                sb.AppendLine($"\"{kvp.Key}\",{kvp.Value.DiasBase},{kvp.Value.DiasExtras}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Genera el string JSON correspondiente a los festivos oficiales.
        /// </summary>
        public static string ExportarFestivosJson(PlanVacaciones datos)
        {
            var sortedFestivos = datos.Festivos.OrderBy(x => x).ToList();
            return JsonSerializer.Serialize(sortedFestivos, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Genera el contenido CSV correspondiente a los festivos oficiales.
        /// </summary>
        public static string ExportarFestivosCsv(PlanVacaciones datos)
        {
            var sb = new StringBuilder();
            foreach (var f in datos.Festivos.OrderBy(x => x))
            {
                sb.AppendLine($"\"{f}\"");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Genera el string JSON correspondiente a las vacaciones asignadas.
        /// </summary>
        public static string ExportarVacacionesJson(PlanVacaciones datos)
        {
            var exportObj = datos.Trabajadores.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Vacaciones
                    .Select(f => {
                        int qYear = (kvp.Value.Imputaciones != null && kvp.Value.Imputaciones.TryGetValue(f, out int y)) 
                            ? y 
                            : int.Parse(f.Substring(6, 4));
                        return new { fecha = f, ano_cupo = qYear };
                    })
                    .OrderBy(x => x.fecha)
                    .ToList()
            );
            return JsonSerializer.Serialize(exportObj, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Genera el contenido CSV correspondiente a las vacaciones asignadas.
        /// </summary>
        public static string ExportarVacacionesCsv(PlanVacaciones datos)
        {
            var sb = new StringBuilder();
            foreach (var kvp in datos.Trabajadores)
            {
                var sortedV = kvp.Value.Vacaciones
                    .Select(f => {
                        int qYear = (kvp.Value.Imputaciones != null && kvp.Value.Imputaciones.TryGetValue(f, out int y)) 
                            ? y 
                            : int.Parse(f.Substring(6, 4));
                        return $"{f}:{qYear}";
                    })
                    .OrderBy(x => x)
                    .ToList();

                if (sortedV.Count > 0)
                {
                    sb.AppendLine($"\"{kvp.Key}\",{string.Join(",", sortedV.Select(x => $"\"{x}\""))}");
                }
                else
                {
                    sb.AppendLine($"\"{kvp.Key}\"");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Genera el contenido CSV de la cuadrícula Gantt para su exportación plana.
        /// </summary>
        public static string ExportarGanttACSV(PlanVacaciones datos, List<string> mesesSecuencia, List<DateTime> fechasEjeX)
        {
            string[] nombresMeses = { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            var sb = new StringBuilder();

            // 1. Fila de Meses
            var filaMeses = new List<string> { "MES" };
            foreach (var mStr in mesesSecuencia)
            {
                var parts = mStr.Split('-');
                int y = int.Parse(parts[0]);
                int m = int.Parse(parts[1]);
                int diasMes = DateTime.DaysInMonth(y, m);
                string etiquetaMes = $"{nombresMeses[m].ToUpper()} {y}";
                filaMeses.Add(etiquetaMes);
                for (int i = 1; i < diasMes; i++)
                {
                    filaMeses.Add("");
                }
            }
            sb.AppendLine(string.Join(",", filaMeses.Select(x => $"\"{x}\"")));

            // 2. Fila de Días
            var filaDias = new List<string> { "TRABAJADOR" };
            foreach (var d in fechasEjeX)
            {
                filaDias.Add(d.Day.ToString());
            }
            sb.AppendLine(string.Join(",", filaDias.Select(x => $"\"{x}\"")));

            // 3. Filas de Trabajadores
            foreach (var kvp in datos.Trabajadores.OrderBy(k => k.Key))
            {
                var filaWorker = new List<string> { kvp.Key };
                foreach (var date in fechasEjeX)
                {
                    string dateStr = $"{date.Day:00}/{date.Month:00}/{date.Year}";
                    bool esWeekend = (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
                    bool esFestivo = datos.Festivos.Contains(dateStr);
                    bool esVacacion = kvp.Value.Vacaciones.Contains(dateStr);

                    if (esVacacion)
                    {
                        filaWorker.Add("V");
                    }
                    else if (esFestivo || esWeekend)
                    {
                        filaWorker.Add("F");
                    }
                    else
                    {
                        filaWorker.Add("");
                    }
                }
                sb.AppendLine(string.Join(",", filaWorker.Select(x => $"\"{x}\"")));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parser auxiliar de cadenas CSV para interpretar comillas y comas adecuadamente.
        /// </summary>
        private static List<List<string>> ParseCsv(string text)
        {
            var result = new List<List<string>>();
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var row = new List<string>();
                bool inQuotes = false;
                var currentToken = new StringBuilder();

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];
                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        row.Add(currentToken.ToString().Trim());
                        currentToken.Clear();
                    }
                    else
                    {
                        currentToken.Append(c);
                    }
                }
                row.Add(currentToken.ToString().Trim());
                result.Add(row);
            }

            return result;
        }

        #region Implementación de IDataManager

        PlanVacaciones IDataManager.CargarDatos() => CargarDatos();
        PlanVacaciones IDataManager.InicializarDatosVacios() => InicializarDatosVacios();
        void IDataManager.GuardarDatos(PlanVacaciones datos) => GuardarDatos(datos);
        ImportResult IDataManager.ImportarDesdeTexto(PlanVacaciones datosActuales, string text, bool esJson) => ImportarDesdeTexto(datosActuales, text, esJson);
        string IDataManager.ExportarTrabajadoresJson(PlanVacaciones datos) => ExportarTrabajadoresJson(datos);
        string IDataManager.ExportarTrabajadoresCsv(PlanVacaciones datos) => ExportarTrabajadoresCsv(datos);
        string IDataManager.ExportarFestivosJson(PlanVacaciones datos) => ExportarFestivosJson(datos);
        string IDataManager.ExportarFestivosCsv(PlanVacaciones datos) => ExportarFestivosCsv(datos);
        string IDataManager.ExportarVacacionesJson(PlanVacaciones datos) => ExportarVacacionesJson(datos);
        string IDataManager.ExportarVacacionesCsv(PlanVacaciones datos) => ExportarVacacionesCsv(datos);
        string IDataManager.ExportarGanttACSV(PlanVacaciones datos, List<string> mesesSecuencia, List<DateTime> fechasEjeX) => ExportarGanttACSV(datos, mesesSecuencia, fechasEjeX);

        #endregion
    }
}
