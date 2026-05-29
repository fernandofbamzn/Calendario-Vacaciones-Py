using System.Collections.Generic;

namespace CalendarioWPF.Data.Entities
{
    public class PlanEntity
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public string TituloPagina { get; set; } = string.Empty;

        public List<FestivoEntity> Festivos { get; set; } = new List<FestivoEntity>();
        public List<TrabajadorEntity> Trabajadores { get; set; } = new List<TrabajadorEntity>();

        // Propiedades almacenadas como JSON en la base de datos
        public string DepartamentosJson { get; set; } = string.Empty;
        public string CierresJson { get; set; } = string.Empty;
        public string IncompatibilidadesJson { get; set; } = string.Empty;
        public string DepartamentosIncompatiblesJson { get; set; } = string.Empty;
        public string DepartamentosColoresJson { get; set; } = string.Empty;
    }
}
