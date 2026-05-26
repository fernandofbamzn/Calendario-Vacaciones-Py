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
