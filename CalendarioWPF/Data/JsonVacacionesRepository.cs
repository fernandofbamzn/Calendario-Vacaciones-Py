using System;
using System.Collections.Generic;
using System.IO;
using CalendarioWPF.Models;
using CalendarioWPF.Services;

namespace CalendarioWPF.Data
{
    /// <summary>
    /// Implementación de IVacacionesRepository basada en el sistema JSON original (DataManager).
    /// </summary>
    public class JsonVacacionesRepository : IVacacionesRepository
    {
        public PlanVacaciones CargarPlan(int year)
        {
            // El sistema JSON original guarda todo en un único archivo,
            // por lo que ignoramos el parámetro 'year' y cargamos lo que haya.
            return DataManager.CargarDatos();
        }

        public void GuardarPlan(PlanVacaciones plan)
        {
            DataManager.GuardarDatos(plan);
        }

        public List<int> ObtenerAñosDisponibles()
        {
            var plan = DataManager.CargarDatos();
            return new List<int> { plan.Year };
        }
    }
}
