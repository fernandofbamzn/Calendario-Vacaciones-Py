using System.Collections.Generic;
using CalendarioWPF.Models;

namespace CalendarioWPF.Data
{
    /// <summary>
    /// Define las operaciones principales de persistencia para el sistema de vacaciones.
    /// Utiliza este repositorio para aislar a la aplicación de la capa de acceso a datos (JSON, SQLite, etc.).
    /// </summary>
    public interface IVacacionesRepository
    {
        /// <summary>
        /// Obtiene un plan de vacaciones completo para un año específico.
        /// </summary>
        /// <param name="year">El año del plan a cargar.</param>
        /// <returns>La instancia del plan de vacaciones, o null si no existe.</returns>
        PlanVacaciones CargarPlan(int year);

        /// <summary>
        /// Guarda o actualiza un plan de vacaciones completo.
        /// </summary>
        /// <param name="plan">El plan de vacaciones a persistir.</param>
        void GuardarPlan(PlanVacaciones plan);

        /// <summary>
        /// Obtiene los años disponibles almacenados en el origen de datos.
        /// </summary>
        /// <returns>Lista de años para los que existe un plan de vacaciones.</returns>
        List<int> ObtenerAñosDisponibles();
    }
}
