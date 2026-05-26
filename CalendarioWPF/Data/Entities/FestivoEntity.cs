namespace CalendarioWPF.Data.Entities
{
    public class FestivoEntity
    {
        public int Id { get; set; }
        public int PlanEntityId { get; set; }
        public PlanEntity Plan { get; set; } = null!;

        public string Fecha { get; set; } = string.Empty;
    }
}
