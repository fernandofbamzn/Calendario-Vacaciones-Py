using System;
using System.Collections.Generic;

namespace CalendarioWPF.Services
{
    /// <summary>
    /// Interfaz para el gestor de datos (DataManager).
    /// Define los métodos para cargar, guardar e importar datos del planificador de vacaciones.
    /// Documentado exhaustivamente para reducir el consumo de tokens en futuras interacciones con agentes.
    /// </summary>
    public interface IDataManager
    {
        /// <summary>
        /// Carga los datos de vacaciones desde el almacenamiento de persistencia (ej. archivo JSON local).
        /// Si el archivo no existe, retorna una instancia de datos vacía configurada con el año actual.
        /// </summary>
        /// <returns>Una instancia de <see cref="PlanVacaciones"/> con los datos cargados.</returns>
        PlanVacaciones CargarDatos();

        /// <summary>
        /// Inicializa y retorna un plan de vacaciones vacío, con valores predeterminados y el año actual.
        /// </summary>
        /// <returns>Un nuevo objeto de tipo <see cref="PlanVacaciones"/> completamente inicializado pero vacío.</returns>
        PlanVacaciones InicializarDatosVacios();

        /// <summary>
        /// Guarda de forma persistente (ej. en el archivo JSON local) el estado actual del planizador de vacaciones.
        /// </summary>
        /// <param name="datos">La instancia de <see cref="PlanVacaciones"/> que contiene toda la información a guardar.</param>
        void GuardarDatos(PlanVacaciones datos);

        /// <summary>
        /// Importa datos a partir de una cadena de texto (JSON o CSV) y los fusiona/reemplaza en el plan actual.
        /// </summary>
        /// <param name="datosActuales">El objeto <see cref="PlanVacaciones"/> actual sobre el que aplicar los cambios.</param>
        /// <param name="text">El texto fuente que se desea importar (contenido del archivo JSON/CSV).</param>
        /// <param name="esJson">Indica si el texto está en formato JSON (<c>true</c>) o CSV (<c>false</c>).</param>
        /// <returns>Un objeto de tipo <see cref="ImportResult"/> con el resultado de la importación y los datos actualizados.</returns>
        ImportResult ImportarDesdeTexto(PlanVacaciones datosActuales, string text, bool esJson);

        /// <summary>
        /// Serializa a formato JSON los datos de configuración de los trabajadores (nombres y límites de días base/extra).
        /// </summary>
        /// <param name="datos">El plan de vacaciones del cual extraer la información.</param>
        /// <returns>Una cadena de texto formateada en JSON con el diccionario de personal.</returns>
        string ExportarTrabajadoresJson(PlanVacaciones datos);

        /// <summary>
        /// Exporta la configuración de días de los trabajadores en formato CSV delimitado por comas.
        /// </summary>
        /// <param name="datos">El plan de vacaciones origen.</param>
        /// <returns>Una cadena en formato CSV.</returns>
        string ExportarTrabajadoresCsv(PlanVacaciones datos);

        /// <summary>
        /// Serializa a formato JSON la lista ordenada de días festivos oficiales registrados en el sistema.
        /// </summary>
        /// <param name="datos">El plan de vacaciones origen.</param>
        /// <returns>Cadena en formato JSON con el listado de fechas de festivos.</returns>
        string ExportarFestivosJson(PlanVacaciones datos);

        /// <summary>
        /// Exporta el listado de días festivos oficiales a formato CSV, con una fecha por línea.
        /// </summary>
        /// <param name="datos">El plan de vacaciones origen.</param>
        /// <returns>Cadena en formato CSV.</returns>
        string ExportarFestivosCsv(PlanVacaciones datos);

        /// <summary>
        /// Serializa a JSON el listado de vacaciones individuales asignadas a cada trabajador.
        /// Cada elemento de la lista del trabajador es un objeto que contiene la fecha y el año de cupo al que se imputa:
        /// { "fecha": "dd/MM/yyyy", "año_cupo": AAAA }
        /// </summary>
        /// <param name="datos">El plan de vacaciones origen.</param>
        /// <returns>Cadena JSON con la relación trabajador-fechas de vacaciones con su respectiva imputación de cupo.</returns>
        string ExportarVacacionesJson(PlanVacaciones datos);

        /// <summary>
        /// Exporta la asignación de vacaciones del personal a un formato CSV para lectura tabular.
        /// Cada vacación se exporta con el formato "fecha:año_cupo" (ej. "dd/MM/yyyy:AAAA") para persistir la imputación.
        /// </summary>
        /// <param name="datos">El plan de vacaciones origen.</param>
        /// <returns>Cadena CSV con la relación de vacaciones y sus respectivos años de cupo.</returns>
        string ExportarVacacionesCsv(PlanVacaciones datos);

        /// <summary>
        /// Genera el contenido de una planilla Gantt en formato de texto CSV plano para su posterior guardado.
        /// </summary>
        /// <param name="datos">El plan de vacaciones con los datos de trabajadores y días pintados.</param>
        /// <param name="mesesSecuencia">Secuencia de meses (ej. "2026-06", "2026-07") que forman la cabecera.</param>
        /// <param name="fechasEjeX">Lista secuencial de fechas completas (días) del eje horizontal.</param>
        /// <returns>Cadena CSV conteniendo el Gantt completo.</returns>
        string ExportarGanttACSV(PlanVacaciones datos, List<string> mesesSecuencia, List<DateTime> fechasEjeX);
    }
}
