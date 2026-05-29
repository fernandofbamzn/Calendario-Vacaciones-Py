/**
 * app.js - Componente principal y punto de entrada de la aplicación
 * 
 * Contiene el componente raíz <App> que:
 * - Gestiona el estado global de la aplicación (datos, trabajador activo, pestaña activa).
 * - Orquesta la carga y persistencia de datos en localStorage.
 * - Implementa la lógica de marcado/desmarcado de vacaciones con detección de incompatibilidades.
 * - Renderiza la barra de trabajadores con selector de departamento.
 * - Coordina las vistas (Calendario, Gantt) y diálogos (Config, Cierre de Empresa).
 * 
 * IMPORTANTE: Este archivo se carga como script global (no ES module).
 * Debe cargarse DESPUÉS de utils.js, services.js y components.js.
 * 
 * Dependencias globales requeridas:
 * - React, ReactDOM (UMD global)
 * - htm → html (enlazado en components.js)
 * - utils.js (DEFAULT_CONFIG, funciones utilitarias, incompatibilidades)
 * - services.js (StorageService, ExportService)
 * - components.js (Header, ConfigDialog, CierreEmpresaDialog, CalendarGrid, GanttGrid, ResumenVacaciones)
 */

// ============================================================================
// COMPONENTE PRINCIPAL: APP
// ============================================================================

/**
 * Componente raíz de la aplicación.
 * Gestiona todo el estado global y coordina la interacción entre componentes.
 */
const App = () => {
    // ---- Estado global ----
    /** Datos completos de la aplicación (estructura de DEFAULT_CONFIG) */
    const [data, setData] = useState(null);
    /** Nombre del trabajador actualmente seleccionado (o "" para "todos") */
    const [activeWorker, setActiveWorker] = useState("");
    /** Controla la visibilidad del diálogo de configuración */
    const [showConfig, setShowConfig] = useState(false);
    /** Controla la visibilidad del diálogo de cierre de empresa */
    const [showCierre, setShowCierre] = useState(false);
    /** Texto del input para añadir un nuevo trabajador */
    const [newWorkerName, setNewWorkerName] = useState("");
    /** Pestaña activa: "calendario" o "gantt" */
    const [activeTab, setActiveTab] = useState("calendario");
    /** Filtro de departamento activo en la vista */
    const [filtroDpto, setFiltroDpto] = useState("");

    // ---- Carga y persistencia automática ----
    /** Carga inicial desde localStorage al montar el componente */
    useEffect(() => { setData(StorageService.loadData()); }, []);
    /** Guarda automáticamente en localStorage cada vez que cambian los datos */
    useEffect(() => { if (data) StorageService.saveData(data); }, [data]);

    // Pantalla de carga mientras se obtienen los datos de localStorage
    if (!data) return html`<div className="p-5 text-center">Cargando...</div>`;

    // ---- Handlers ----

    /**
     * Importa datos desde un archivo seleccionado por el usuario (JSON o CSV).
     * Usa la función importarDataInteligente para detectar el tipo de datos
     * y fusionarlos con los existentes.
     * 
     * @param {Event} e - Evento change del input file.
     */
    const handleImport = (e) => {
        const f = e.target.files[0];
        if (!f) return;
        const reader = new FileReader();
        reader.onload = (evt) => {
            try {
                const isJson = f.name.toLowerCase().endsWith(".json");
                const newData = importarDataInteligente(data, evt.target.result, isJson);
                setData(newData);
                alert("Datos importados exitosamente.");
            } catch (err) {
                console.error(err);
                alert("Error al importar el archivo: " + err.message);
            }
        };
        reader.readAsText(f);
        e.target.value = null;
    };

    /**
     * Marca o desmarca un día de vacaciones para un trabajador.
     * Incluye validaciones:
     * - No permite marcar fines de semana ni festivos.
     * - Muestra alerta si se detectan incompatibilidades (no bloqueante).
     * 
     * @param {string} dateStr - Fecha en formato "dd/MM/yyyy".
     * @param {string|null} forceMode - 'add', 'remove' o null (toggle automático).
     * @param {string|null} specificWorker - Nombre del trabajador (si null, usa activeWorker).
     */
    const toggleDay = (dateStr, forceMode = null, specificWorker = null) => {
        const workerName = specificWorker || activeWorker;
        if (!workerName) return;
        const worker = data.trabajadores[workerName];
        const vacs = [...worker.vacaciones];
        const idx = vacs.indexOf(dateStr);

        // Validar que no sea fin de semana ni festivo
        const day = new Date(dateStr.split("/").reverse().join("-")).getDay();
        const festivosDelTrabajador = obtenerFestivosTrabajador(workerName, data);
        if (day === 0 || day === 6 || festivosDelTrabajador.includes(dateStr)) return;

        if (forceMode === 'add' && idx === -1) {
            vacs.push(dateStr);
        } else if (forceMode === 'remove' && idx > -1) {
            vacs.splice(idx, 1);
        } else if (!forceMode) {
            if (idx > -1) vacs.splice(idx, 1);
            else vacs.push(dateStr);
        }

        // Comprobar incompatibilidades al añadir un día
        if ((forceMode === 'add' || (!forceMode && idx === -1)) && data.incompatibilidades) {
            const conflictos = comprobarIncompatibilidades(workerName, dateStr, data.trabajadores, data.incompatibilidades, data.cierres || {});
            if (conflictos.length > 0) {
                // Mostrar un toast/alerta no bloqueante
                mostrarToastIncompatibilidad(workerName, dateStr, conflictos);
            }
        }

        setData(prev => ({ ...prev, trabajadores: { ...prev.trabajadores, [workerName]: { ...prev.trabajadores[workerName], vacaciones: vacs } } }));
    };

    /**
     * Añade un nuevo trabajador a la lista.
     * Le asigna el departamento "General" y valores por defecto.
     */
    const addWorker = () => {
        const name = newWorkerName.trim();
        if (!name) return;
        if (data.trabajadores[name]) return alert("Ya existe un trabajador con ese nombre.");
        setData({ ...data, trabajadores: { ...data.trabajadores, [name]: { vacaciones: [], dias_base: 22, dias_extras: 0, departamento: filtroDpto || "General", imputaciones: {} } } });
        setActiveWorker(name);
        setNewWorkerName("");
    };

    /**
     * Elimina el trabajador actualmente seleccionado (con confirmación).
     * También limpia las reglas de incompatibilidad que lo referencien.
     */
    const delWorker = () => {
        if (!activeWorker) return;
        if (!confirm(`¿Estás seguro de eliminar a ${activeWorker}?`)) return;
        const t = { ...data.trabajadores };
        delete t[activeWorker];
        // Limpiar incompatibilidades que referencien al trabajador eliminado
        const incomp = { ...data.incompatibilidades };
        delete incomp[activeWorker];
        Object.keys(incomp).forEach(k => {
            incomp[k] = incomp[k].filter(x => x !== activeWorker);
            if (incomp[k].length === 0) delete incomp[k];
        });
        setData({ ...data, trabajadores: t, incompatibilidades: incomp });
        setActiveWorker("");
    };

    /**
     * Exporta los datos del trabajador activo como archivo JSON individual.
     */
    const exportTrabajadorJson = () => {
        if (!activeWorker) return alert("Selecciona un trabajador");
        ExportService.exportTrabajadorJson(data, activeWorker);
    };

    /**
     * Cambia el departamento del trabajador activo.
     * @param {string} newDept - Nombre del nuevo departamento.
     */
    const changeDepartamento = (newDept) => {
        if (!activeWorker) return;
        setData({
            ...data,
            trabajadores: {
                ...data.trabajadores,
                [activeWorker]: { ...data.trabajadores[activeWorker], departamento: newDept }
            }
        });
    };

    // ---- Cómputo del trabajador activo ----
    const festivosActiveWorker = activeWorker ? obtenerFestivosTrabajador(activeWorker, data) : [];
    const cons = activeWorker ? contarDiasConsumidos(data.trabajadores[activeWorker].vacaciones, festivosActiveWorker) : 0;
    const lim = activeWorker ? (data.trabajadores[activeWorker].dias_base + data.trabajadores[activeWorker].dias_extras) : 0;
    const departamentos = data.departamentos || ["General"];

    // ---- Renderizado ----
    return html`
        <div style=${{ paddingBottom: '100px' }}>
            <${Header} 
                data=${data}
                onImportJson=${handleImport}
                onOpenConfig=${() => setShowConfig(true)}
                filtroDpto=${filtroDpto}
            />
            
            <div className="container-fluid px-4">
                <!-- Panel de gestión de trabajador -->
                <div className="card shadow-sm mb-4 border-0">
                    <div className="card-body bg-white rounded">
                        <div className="row align-items-center gy-3">
                            <!-- Selector de departamento para filtrar -->
                            <div className="col-12 col-md-auto d-flex gap-2 align-items-center">
                                <span className="text-muted small fw-bold">Dpto:</span>
                                <select className="form-select form-select-sm" style=${{ minWidth: '130px' }} value=${filtroDpto} onChange=${e => { setFiltroDpto(e.target.value); setActiveWorker(""); }}>
                                    <option value="">Todos</option>
                                    ${departamentos.map(d => html`<option key=${d} value=${d}>${d}</option>`)}
                                </select>
                            </div>

                            <!-- Selector de trabajador activo -->
                            <div className="col-12 col-md-auto d-flex gap-2 align-items-center">
                                <select className="form-select fw-bold text-primary" style=${{ minWidth: '250px' }} value=${activeWorker} onChange=${e => setActiveWorker(e.target.value)}>
                                    <option value="">(Todos los trabajadores)</option>
                                    ${Object.keys(data.trabajadores)
                                        .filter(w => !filtroDpto || (data.trabajadores[w].departamento || "General") === filtroDpto)
                                        .sort().map(w => html`<option key=${w} value=${w}>${w}</option>`)}
                                </select>
                                ${activeWorker ? html`
                                    <button className="btn btn-outline-danger" title="Eliminar trabajador" onClick=${delWorker}><i className="bi bi-trash"></i></button>
                                    <button className="btn btn-outline-secondary" title="Exportar JSON Individual" onClick=${exportTrabajadorJson}><i className="bi bi-download"></i></button>
                                ` : null}
                            </div>
                            <!-- Input para nuevo trabajador -->
                            <div className="col-12 col-md-auto d-flex gap-2">
                                <input className="form-control" placeholder="Nuevo trabajador..." value=${newWorkerName} onChange=${e => setNewWorkerName(e.target.value)} onKeyDown=${e => e.key === 'Enter' && addWorker()} />
                                <button className="btn btn-success" onClick=${addWorker}><i className="bi bi-person-plus"></i></button>
                            </div>
                            
                            ${activeWorker ? html`
                                <!-- Estadísticas del trabajador activo -->
                                <div className="col-12 col-md-auto ms-md-auto d-flex align-items-center gap-4 bg-light p-2 rounded border">
                                    <!-- Selector de departamento -->
                                    <div>
                                        <div className="text-muted small fw-bold text-uppercase">Departamento</div>
                                        <select className="form-select form-select-sm" style=${{ minWidth: '120px' }} 
                                                value=${data.trabajadores[activeWorker].departamento || "General"}
                                                onChange=${e => changeDepartamento(e.target.value)}>
                                            ${departamentos.map(d => html`<option key=${d} value=${d}>${d}</option>`)}
                                        </select>
                                    </div>
                                    <!-- Cómputo de días -->
                                    <div className="text-center">
                                        <div className="text-muted small fw-bold text-uppercase">Días Consumidos</div>
                                        <div className=${`fs-5 fw-bold ${cons > lim ? "text-danger" : "text-primary"}`}>${cons} / ${lim}</div>
                                    </div>
                                    <!-- Inputs de días base/extras -->
                                    <div className="d-flex align-items-center gap-2">
                                        <div>
                                            <div className="text-muted small fw-bold">Base</div>
                                            <input type="number" className="form-control form-control-sm text-center" style=${{ width: '60px' }} value=${data.trabajadores[activeWorker].dias_base} 
                                                   onChange=${e => setData({ ...data, trabajadores: { ...data.trabajadores, [activeWorker]: { ...data.trabajadores[activeWorker], dias_base: parseInt(e.target.value) } } })}/>
                                        </div>
                                        <div>
                                            <div className="text-muted small fw-bold">Extras</div>
                                            <input type="number" className="form-control form-control-sm text-center" style=${{ width: '60px' }} value=${data.trabajadores[activeWorker].dias_extras} 
                                                   onChange=${e => setData({ ...data, trabajadores: { ...data.trabajadores, [activeWorker]: { ...data.trabajadores[activeWorker], dias_extras: parseInt(e.target.value) } } })}/>
                                        </div>
                                    </div>
                                </div>
                            ` : html`
                                <div className="col-12 col-md-auto ms-md-auto text-muted fst-italic">
                                    <i className="bi bi-info-circle me-1"></i> Selecciona un trabajador para ver sus estadísticas o usa la Vista Gantt.
                                </div>
                            `}
                        </div>
                    </div>
                </div>

                <!-- Pestañas de vista -->
                <ul className="nav nav-tabs fw-bold">
                    <li className="nav-item">
                        <button className=${`nav-link ${activeTab === 'calendario' ? 'active' : ''}`} onClick=${() => setActiveTab("calendario")}>
                            <i className="bi bi-calendar3 me-2"></i>Vista Calendario Mensual
                        </button>
                    </li>
                    <li className="nav-item">
                        <button className=${`nav-link ${activeTab === 'gantt' ? 'active' : ''}`} onClick=${() => setActiveTab("gantt")}>
                            <i className="bi bi-bar-chart-steps me-2"></i>Vista Gantt (Tabla)
                        </button>
                    </li>
                    <!-- Botón de cierre de empresa en las pestañas -->
                    <li className="nav-item ms-auto">
                        <button className="nav-link text-warning" onClick=${() => setShowCierre(true)} title="Aplicar cierre de empresa/departamento">
                            <i className="bi bi-calendar-x me-1"></i>Cierre de Empresa
                        </button>
                    </li>
                </ul>

                ${activeTab === "calendario"
                    ? html`<${CalendarGrid} data=${data} activeWorker=${activeWorker} onToggleDay=${toggleDay} filtroDpto=${filtroDpto} />`
                    : html`<${GanttGrid} data=${data} activeWorker=${activeWorker} onToggleDay=${toggleDay} filtroDpto=${filtroDpto} />`
                }

                <${ResumenVacaciones} data=${data} filtroDpto=${filtroDpto} />
            </div>

            <!-- Diálogos modales -->
            <${ConfigDialog} show=${showConfig} data=${data} onClose=${() => setShowConfig(false)} onSave=${d => { setData(d); setShowConfig(false); }} />
            <${CierreEmpresaDialog} show=${showCierre} data=${data} onClose=${() => setShowCierre(false)} onApply=${d => { setData(d); setShowCierre(false); }} />
        </div>
    `;
};

// ============================================================================
// FUNCIÓN AUXILIAR: TOAST DE INCOMPATIBILIDAD
// ============================================================================

/**
 * Muestra una alerta visual no bloqueante (toast) cuando se detecta
 * una incompatibilidad de vacaciones entre trabajadores.
 * El toast se muestra durante 5 segundos y desaparece automáticamente.
 * 
 * @param {string} trabajador - Nombre del trabajador que está marcando vacaciones.
 * @param {string} fecha - Fecha en formato "dd/MM/yyyy".
 * @param {string[]} conflictos - Nombres de los trabajadores incompatibles que coinciden.
 */
function mostrarToastIncompatibilidad(trabajador, fecha, conflictos) {
    // Crear contenedor de toasts si no existe
    let container = document.getElementById('toast-container-incomp');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container-incomp';
        container.style.cssText = 'position:fixed;top:20px;right:20px;z-index:9999;max-width:400px;';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = 'alert alert-warning alert-dismissible fade show shadow';
    toast.style.cssText = 'animation:fadeIn 0.3s;margin-bottom:8px;';
    toast.innerHTML = `
        <strong><i class="bi bi-exclamation-triangle me-1"></i>Incompatibilidad</strong><br/>
        <small>${trabajador} coincide el ${fecha} con: <strong>${conflictos.join(", ")}</strong></small>
        <button type="button" class="btn-close btn-close-sm" data-bs-dismiss="alert"></button>
    `;
    container.appendChild(toast);

    // Eliminar automáticamente tras 5 segundos
    setTimeout(() => {
        if (toast.parentNode) {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }
    }, 5000);
}

// ============================================================================
// INICIALIZACIÓN DE LA APLICACIÓN
// ============================================================================

/** Punto de entrada: renderiza el componente App en el elemento #root */
const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(html`<${App} />`);
