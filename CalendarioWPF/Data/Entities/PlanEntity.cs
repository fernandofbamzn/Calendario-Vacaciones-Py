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
    }
}
