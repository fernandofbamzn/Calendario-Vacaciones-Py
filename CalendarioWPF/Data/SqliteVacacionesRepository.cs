using System;
using System.Collections.Generic;
using System.Linq;
using CalendarioWPF.Data.Entities;
using CalendarioWPF.Models;
using Microsoft.EntityFrameworkCore;

namespace CalendarioWPF.Data
{
    /// <summary>
    /// Implementación de persistencia utilizando SQLite y Entity Framework Core.
    /// Adapta el modelo relacional (Entities) al modelo de dominio (PlanVacaciones).
    /// </summary>
    public class SqliteVacacionesRepository : IVacacionesRepository
    {
        private readonly VacacionesDbContext _context;

        public SqliteVacacionesRepository()
        {
            _context = new VacacionesDbContext();
            _context.Database.EnsureCreated();
            EnsureColumnsExist();
        }

        private void EnsureColumnsExist()
        {
            var conn = _context.Database.GetDbConnection();
            bool wasOpen = conn.State == System.Data.ConnectionState.Open;
            if (!wasOpen) conn.Open();

            try
            {
                using var command = conn.CreateCommand();
                command.CommandText = "PRAGMA table_info(Planes);";
                using var reader = command.ExecuteReader();
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1));
                }
                reader.Close();

                string[] requiredColumns = { "DepartamentosJson", "CierresJson", "IncompatibilidadesJson", "DepartamentosIncompatiblesJson", "DepartamentosColoresJson" };
                foreach (var col in requiredColumns)
                {
                    if (!columns.Contains(col))
                    {
                        using var alterCmd = conn.CreateCommand();
                        alterCmd.CommandText = $"ALTER TABLE Planes ADD COLUMN {col} TEXT NULL;";
                        alterCmd.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                if (!wasOpen) conn.Close();
            }
        }

        public PlanVacaciones CargarPlan(int year)
        {
            var planEntity = _context.Planes
                .Include(p => p.Festivos)
                .Include(p => p.Trabajadores).ThenInclude(t => t.Vacaciones)
                .Include(p => p.Trabajadores).ThenInclude(t => t.Imputaciones)
                .FirstOrDefault(p => p.Year == year);

            if (planEntity == null)
            {
                return new PlanVacaciones { Year = year };
            }

            var plan = new PlanVacaciones
            {
                Year = planEntity.Year,
                TituloPagina = planEntity.TituloPagina
            };

            // Deserialize complex properties if they exist
            if (!string.IsNullOrEmpty(planEntity.DepartamentosJson))
                plan.Departamentos = System.Text.Json.JsonSerializer.Deserialize<List<string>>(planEntity.DepartamentosJson) ?? new List<string>() { "General" };
            
            if (!string.IsNullOrEmpty(planEntity.CierresJson))
                plan.Cierres = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(planEntity.CierresJson) ?? new Dictionary<string, List<string>>();

            if (!string.IsNullOrEmpty(planEntity.IncompatibilidadesJson))
                plan.Incompatibilidades = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(planEntity.IncompatibilidadesJson) ?? new Dictionary<string, List<string>>();

            if (!string.IsNullOrEmpty(planEntity.DepartamentosIncompatiblesJson))
                plan.DepartamentosIncompatibles = System.Text.Json.JsonSerializer.Deserialize<List<string>>(planEntity.DepartamentosIncompatiblesJson) ?? new List<string>();

            if (!string.IsNullOrEmpty(planEntity.DepartamentosColoresJson))
                plan.DepartamentosColores = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(planEntity.DepartamentosColoresJson) ?? new Dictionary<string, string>();

            plan.Festivos.AddRange(planEntity.Festivos.Select(f => f.Fecha));

            foreach (var t in planEntity.Trabajadores)
            {
                var info = new InfoTrabajador
                {
                    Departamento = t.Departamento,
                    DiasBase = t.DiasBase,
                    DiasExtras = t.DiasExtras
                };
                info.Vacaciones.AddRange(t.Vacaciones.Select(v => v.Fecha));
                foreach (var imp in t.Imputaciones)
                {
                    info.Imputaciones[imp.Fecha] = imp.YearCupo;
                }
                plan.Trabajadores[t.Nombre] = info;
            }

            return plan;
        }

        public void GuardarPlan(PlanVacaciones plan)
        {
            var planEntity = _context.Planes
                .Include(p => p.Festivos)
                .Include(p => p.Trabajadores).ThenInclude(t => t.Vacaciones)
                .Include(p => p.Trabajadores).ThenInclude(t => t.Imputaciones)
                .FirstOrDefault(p => p.Year == plan.Year);

            if (planEntity == null)
            {
                planEntity = new PlanEntity { Year = plan.Year };
                _context.Planes.Add(planEntity);
            }

            planEntity.TituloPagina = plan.TituloPagina;

            // Serialize complex properties
            planEntity.DepartamentosJson = System.Text.Json.JsonSerializer.Serialize(plan.Departamentos);
            planEntity.CierresJson = System.Text.Json.JsonSerializer.Serialize(plan.Cierres);
            planEntity.IncompatibilidadesJson = System.Text.Json.JsonSerializer.Serialize(plan.Incompatibilidades);
            planEntity.DepartamentosIncompatiblesJson = System.Text.Json.JsonSerializer.Serialize(plan.DepartamentosIncompatibles);
            planEntity.DepartamentosColoresJson = System.Text.Json.JsonSerializer.Serialize(plan.DepartamentosColores);

            // Update Festivos
            _context.Festivos.RemoveRange(planEntity.Festivos);
            planEntity.Festivos = plan.Festivos.Select(f => new FestivoEntity { Fecha = f }).ToList();

            // Update Trabajadores
            _context.Trabajadores.RemoveRange(planEntity.Trabajadores);
            var nuevosTrabajadores = new List<TrabajadorEntity>();
            
            foreach (var kvp in plan.Trabajadores)
            {
                var info = kvp.Value;
                var tEntity = new TrabajadorEntity
                {
                    Nombre = kvp.Key,
                    Departamento = info.Departamento,
                    DiasBase = info.DiasBase,
                    DiasExtras = info.DiasExtras,
                    Vacaciones = info.Vacaciones.Select(v => new VacacionEntity { Fecha = v }).ToList(),
                    Imputaciones = info.Imputaciones.Select(i => new ImputacionEntity { Fecha = i.Key, YearCupo = i.Value }).ToList()
                };
                nuevosTrabajadores.Add(tEntity);
            }
            planEntity.Trabajadores = nuevosTrabajadores;

            _context.SaveChanges();
        }

        public List<int> ObtenerAñosDisponibles()
        {
            return _context.Planes.Select(p => p.Year).Distinct().OrderBy(y => y).ToList();
        }
    }
}
