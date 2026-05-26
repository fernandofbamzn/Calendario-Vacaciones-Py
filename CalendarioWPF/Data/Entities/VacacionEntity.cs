namespace CalendarioWPF.Data.Entities
{
    public class VacacionEntity
    {
        public int Id { get; set; }
        public int TrabajadorEntityId { get; set; }
        public TrabajadorEntity Trabajador { get; set; } = null!;

        public string Fecha { get; set; } = string.Empty;
    }
}
