using System.Collections.Generic;

namespace CalendarioWPF.Data.Entities
{
    public class TrabajadorEntity
    {
        public int Id { get; set; }
        public int PlanEntityId { get; set; }
        public PlanEntity Plan { get; set; } = null!;

        public string Nombre { get; set; } = string.Empty;
        public string Departamento { get; set; } = "General";
        public int DiasBase { get; set; }
        public int DiasExtras { get; set; }

        public List<VacacionEntity> Vacaciones { get; set; } = new List<VacacionEntity>();
        public List<ImputacionEntity> Imputaciones { get; set; } = new List<ImputacionEntity>();
    }
}
