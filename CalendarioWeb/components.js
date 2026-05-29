/**
 * components.js - Componentes React del Calendario de Vacaciones
 * 
 * Contiene todos los componentes de interfaz de usuario renderizados con React + HTM:
 * - Header:              Barra de navegación superior con botones de importar/exportar.
 * - ConfigDialog:        Modal de configuración general (festivos, meses, PDF, departamentos, incompatibilidades).
 * - CierreEmpresaDialog: Modal para aplicar cierres de empresa/departamento en lote.
 * - CalendarGrid:        Vista de calendario mensual con drag & drop para asignar vacaciones.
 * - GanttGrid:           Vista Gantt (tabla horizontal) con drag & drop por trabajador.
 * - ResumenVacaciones:   Resumen anual con cómputo de días y alertas de incompatibilidad.
 * 
 * IMPORTANTE: Este archivo se carga como script global (no ES module)
 * para evitar restricciones de CORS al abrir index.html directamente
 * desde el sistema de archivos (protocolo file://).
 * 
 * Dependencias globales requeridas (deben cargarse antes que este archivo):
 * - React, ReactDOM (UMD global)
 * - htm (UMD global)
 * - utils.js (constantes y funciones utilitarias)
 * - services.js (StorageService, HolidayService, ExportService)
 */

// Enlazar htm con React.createElement para usar template literals como JSX
const html = htm.bind(React.createElement);
const { useState, useEffect, useMemo, useRef, Fragment } = React;

// ============================================================================
// COMPONENTE: HEADER (Barra de navegación)
// ============================================================================

/**
 * Barra de navegación superior de la aplicación.
 * Contiene el título, botones de configuración, menú desplegable de exportación
 * y el botón para importar archivos JSON/CSV.
 * 
 * @param {Object} props
 * @param {Object} props.data - Datos actuales de la aplicación (para las funciones de exportación).
 * @param {Function} props.onImportJson - Callback invocado al seleccionar un archivo para importar.
 * @param {Function} props.onOpenConfig - Callback para abrir el diálogo de configuración.
 */
const Header = ({ data, onImportJson, onOpenConfig, filtroDpto }) => html`
    <nav className="navbar navbar-expand-lg navbar-dark bg-primary mb-4 shadow-sm">
        <div className="container-fluid">
            <span className="navbar-brand fw-bold"><i className="bi bi-calendar-check me-2"></i>Gestor de Vacaciones</span>
            <div className="d-flex gap-2">
                <button className="btn btn-light btn-sm" onClick=${onOpenConfig}><i className="bi bi-gear me-1"></i> Config</button>
                <div className="dropdown">
                    <button className="btn btn-light btn-sm dropdown-toggle" type="button" data-bs-toggle="dropdown"><i className="bi bi-cloud-arrow-down me-1"></i> Exportar</button>
                    <ul className="dropdown-menu dropdown-menu-end shadow">
                        <li><h6 className="dropdown-header">Datos JSON</h6></li>
                        <li><button className="dropdown-item" onClick=${() => StorageService.exportJson(data)}><i className="bi bi-filetype-json me-2"></i>JSON Consolidado Completo</button></li>
                        <li><button className="dropdown-item" onClick=${() => ExportService.exportTrabajadoresJson(data)}><i className="bi bi-people me-2"></i>Exportar Configuración Trabajadores</button></li>
                        <li><button className="dropdown-item" onClick=${() => ExportService.exportFestivosJson(data)}><i className="bi bi-calendar-event me-2"></i>Exportar Festivos Oficiales</button></li>
                        <li><button className="dropdown-item" onClick=${() => ExportService.exportVacacionesJson(data)}><i className="bi bi-calendar-check me-2"></i>Exportar Vacaciones (Asignaciones)</button></li>
                        <li><hr className="dropdown-divider"/></li>
                        <li><h6 className="dropdown-header">Datos CSV / Excel</h6></li>
                        <li><button className="dropdown-item" onClick=${() => ExportService.exportToExcel(data)}><i className="bi bi-file-earmark-excel text-success me-2"></i>Trabajadores (Configuración CSV)</button></li>
                        <li><hr className="dropdown-divider"/></li>
                        <li><h6 className="dropdown-header">Documentos PDF</h6></li>
                        <li><button className="dropdown-item" onClick=${() => ExportService.exportToPdfMensual(data, filtroDpto)}><i className="bi bi-file-pdf text-danger me-2"></i>PDF (Vista Mensual)</button></li>
                        <li><button className="dropdown-item" onClick=${() => ExportService.exportToPdfGantt(data, filtroDpto)}><i className="bi bi-file-pdf text-danger me-2"></i>PDF (Vista Gantt)</button></li>
                    </ul>
                </div>
                <label className="btn btn-light btn-sm mb-0">
                    <i className="bi bi-cloud-arrow-up me-1"></i> Importar
                    <input type="file" accept=".json,.csv" style=${{ display: 'none' }} onChange=${onImportJson} />
                </label>
            </div>
        </div>
    </nav>
`;

// ============================================================================
// COMPONENTE: CONFIGDIALOG (Diálogo de configuración)
// ============================================================================

/**
 * Diálogo modal de configuración general de la aplicación.
 * Permite gestionar: título, año, comunidad autónoma, festivos,
 * opciones de PDF, meses a mostrar, departamentos e incompatibilidades.
 * 
 * @param {Object} props
 * @param {boolean} props.show - Si true, muestra el diálogo.
 * @param {Object} props.data - Datos actuales de la aplicación.
 * @param {Function} props.onClose - Callback para cerrar sin guardar.
 * @param {Function} props.onSave - Callback que recibe los datos modificados al guardar.
 */
const ConfigDialog = ({ show, data, onClose, onSave }) => {
    if (!show) return null;

    // Estado local del diálogo (copia de los datos para edición)
    const [local, setLocal] = useState({
        ...data,
        meses_a_mostrar: data.meses_a_mostrar || [6, 7, 8, 9],
        departamentos: data.departamentos || ["General"],
        incompatibilidades: data.incompatibilidades || {}
    });
    const [newHoliday, setNewHoliday] = useState("");
    const [newHolidayDept, setNewHolidayDept] = useState("Global");
    const [newDeptName, setNewDeptName] = useState("");
    const [configTab, setConfigTab] = useState("general");
    // Estado para gestión de incompatibilidades
    const [selectedIncompWorker, setSelectedIncompWorker] = useState("");
    const [selectedIncompTarget, setSelectedIncompTarget] = useState("");
    const [selectedIncompDept, setSelectedIncompDept] = useState("");
    
    // Estado para gestión de personal
    const [selectedWorkers, setSelectedWorkers] = useState([]);
    const [batchBase, setBatchBase] = useState(22);
    const [batchExtra, setBatchExtra] = useState(0);
    const [batchDept, setBatchDept] = useState("General");
    const [newWorkerName, setNewWorkerName] = useState("");

    /**
     * Importa festivos desde la API OpenHolidays para la comunidad y año seleccionados.
     */
    const fetchHolidays = async () => {
        const f = await HolidayService.fetchHolidays(local.comunidadAutonoma, local.year);
        setLocal({ ...local, festivos: f });
        alert(`Festivos importados para ${local.comunidadAutonoma}`);
    };

    /**
     * Alterna un mes en la lista de meses visibles.
     * @param {number} mesIdx - Número de mes (1-12).
     */
    const toggleMes = (mesIdx) => {
        let ms = [...local.meses_a_mostrar];
        if (ms.includes(mesIdx)) ms = ms.filter(x => x !== mesIdx);
        else ms.push(mesIdx);
        ms.sort((a, b) => a - b);
        setLocal({ ...local, meses_a_mostrar: ms });
    };

    /**
     * Añade un festivo manual desde el selector de fecha.
     */
    const addManualHoliday = () => {
        if (!newHoliday) return;
        const [y, m, d] = newHoliday.split("-");
        if (y && m && d) {
            const dateStr = `${d}/${m}/${y}`;
            
            if (newHolidayDept === "Global") {
                if (!local.festivos.includes(dateStr)) {
                    setLocal({ ...local, festivos: [...local.festivos, dateStr] });
                }
            } else {
                setLocal(prev => {
                    const next = {...prev};
                    if (!next.festivosDepartamento) next.festivosDepartamento = {};
                    if (!next.festivosDepartamento[newHolidayDept]) next.festivosDepartamento[newHolidayDept] = [];
                    if (!next.festivosDepartamento[newHolidayDept].includes(dateStr)) {
                        next.festivosDepartamento[newHolidayDept] = [...next.festivosDepartamento[newHolidayDept], dateStr];
                    }
                    return next;
                });
            }
            setNewHoliday("");
        }
    };

    const removeHoliday = (dateStr, isGlobal, deptName) => {
        if (isGlobal) {
            setLocal({ ...local, festivos: local.festivos.filter(x => x !== dateStr) });
        } else {
            setLocal(prev => {
                const next = {...prev};
                if (next.festivosDepartamento && next.festivosDepartamento[deptName]) {
                    next.festivosDepartamento[deptName] = next.festivosDepartamento[deptName].filter(x => x !== dateStr);
                }
                return next;
            });
        }
    };

    /**
     * Añade un nuevo departamento a la lista gestionable.
     */
    const addDepartamento = () => {
        const name = newDeptName.trim();
        if (!name) return;
        if (local.departamentos.includes(name)) {
            alert("Ya existe un departamento con ese nombre.");
            return;
        }
        setLocal({ ...local, departamentos: [...local.departamentos, name] });
        setNewDeptName("");
    };

    /**
     * Elimina un departamento de la lista.
     * Los trabajadores que lo tenían asignado se reasignan a "General".
     * @param {string} dept - Nombre del departamento a eliminar.
     */
    const removeDepartamento = (dept) => {
        if (dept === "General") {
            alert("No se puede eliminar el departamento 'General'.");
            return;
        }
        // Reasignar trabajadores del departamento eliminado a "General"
        const updatedTrabajadores = { ...local.trabajadores };
        Object.keys(updatedTrabajadores).forEach(w => {
            if (updatedTrabajadores[w].departamento === dept) {
                updatedTrabajadores[w] = { ...updatedTrabajadores[w], departamento: "General" };
            }
        });
        setLocal({
            ...local,
            departamentos: local.departamentos.filter(d => d !== dept),
            trabajadores: updatedTrabajadores
        });
    };

    /**
     * Añade una regla de incompatibilidad entre un trabajador y otro.
     * La regla es bidireccional: si A es incompatible con B, B es incompatible con A.
     */
    const addIncompatibilidadIndividual = () => {
        if (!selectedIncompWorker || !selectedIncompTarget || selectedIncompWorker === selectedIncompTarget) return;
        const incomp = { ...local.incompatibilidades };
        // Añadir en ambas direcciones
        if (!incomp[selectedIncompWorker]) incomp[selectedIncompWorker] = [];
        if (!incomp[selectedIncompTarget]) incomp[selectedIncompTarget] = [];
        if (!incomp[selectedIncompWorker].includes(selectedIncompTarget)) {
            incomp[selectedIncompWorker] = [...incomp[selectedIncompWorker], selectedIncompTarget];
        }
        if (!incomp[selectedIncompTarget].includes(selectedIncompWorker)) {
            incomp[selectedIncompTarget] = [...incomp[selectedIncompTarget], selectedIncompWorker];
        }
        setLocal({ ...local, incompatibilidades: incomp });
        setSelectedIncompTarget("");
    };

    /**
     * Aplica incompatibilidades mutuas entre todos los trabajadores de un departamento.
     * Cada trabajador del departamento será incompatible con todos los demás del mismo departamento.
     */
    const addIncompatibilidadDepartamento = () => {
        if (!selectedIncompDept) return;
        const miembros = Object.keys(local.trabajadores).filter(
            w => (local.trabajadores[w].departamento || "General") === selectedIncompDept
        );
        if (miembros.length < 2) {
            alert("El departamento necesita al menos 2 trabajadores para crear incompatibilidades.");
            return;
        }
        const incomp = { ...local.incompatibilidades };
        // Crear incompatibilidades mutuas entre todos los miembros
        miembros.forEach(a => {
            if (!incomp[a]) incomp[a] = [];
            miembros.forEach(b => {
                if (a !== b && !incomp[a].includes(b)) {
                    incomp[a] = [...incomp[a], b];
                }
            });
        });
        
        const deptIncomp = local.departamentos_incompatibles ? [...local.departamentos_incompatibles] : [];
        if (!deptIncomp.includes(selectedIncompDept)) {
            deptIncomp.push(selectedIncompDept);
        }

        setLocal({ ...local, incompatibilidades: incomp, departamentos_incompatibles: deptIncomp });
        alert(`Incompatibilidades aplicadas entre ${miembros.length} miembros de "${selectedIncompDept}".`);
    };

    /**
     * Elimina una regla de incompatibilidad entre dos trabajadores (bidireccional).
     * @param {string} worker - Nombre del primer trabajador.
     * @param {string} target - Nombre del segundo trabajador.
     */
    const removeIncompatibilidad = (worker, target) => {
        const incomp = { ...local.incompatibilidades };
        if (incomp[worker]) {
            incomp[worker] = incomp[worker].filter(x => x !== target);
            if (incomp[worker].length === 0) delete incomp[worker];
        }
        if (incomp[target]) {
            incomp[target] = incomp[target].filter(x => x !== worker);
            if (incomp[target].length === 0) delete incomp[target];
        }
        setLocal({ ...local, incompatibilidades: incomp });
    };

    // Funciones de gestión de personal
    const addTrabajador = () => {
        const name = newWorkerName.trim();
        if (!name || local.trabajadores[name]) return;
        setLocal({
            ...local,
            trabajadores: {
                ...local.trabajadores,
                [name]: {
                    vacaciones: [],
                    departamento: "General",
                    diasBase: 22,
                    diasExtras: 0
                }
            }
        });
        setNewWorkerName("");
    };

    const removeSelectedWorkers = () => {
        if (selectedWorkers.length === 0) return;
        if (!confirm("¿Eliminar trabajadores seleccionados?")) return;
        const newTrabajadores = { ...local.trabajadores };
        const incomp = { ...local.incompatibilidades };
        selectedWorkers.forEach(w => {
            delete newTrabajadores[w];
            if (incomp[w]) delete incomp[w];
            Object.keys(incomp).forEach(k => {
                incomp[k] = incomp[k].filter(x => x !== w);
                if (incomp[k].length === 0) delete incomp[k];
            });
        });
        setLocal({ ...local, trabajadores: newTrabajadores, incompatibilidades: incomp });
        setSelectedWorkers([]);
    };

    const toggleWorkerSelection = (w) => {
        if (selectedWorkers.includes(w)) setSelectedWorkers(selectedWorkers.filter(x => x !== w));
        else setSelectedWorkers([...selectedWorkers, w]);
    };

    const applyBatchUpdate = (field, value) => {
        if (selectedWorkers.length === 0) return;
        const newTrabajadores = { ...local.trabajadores };
        const incomp = { ...local.incompatibilidades };
        const cierres = { ...local.cierres };

        selectedWorkers.forEach(w => {
            if (field === 'departamento' && value !== newTrabajadores[w].departamento) {
                // Heredar incompatibilidades si el departamento las tiene
                const isDeptIncomp = local.departamentos_incompatibles && local.departamentos_incompatibles.includes(value);
                if (isDeptIncomp) {
                    const miembros = Object.keys(newTrabajadores).filter(x => newTrabajadores[x].departamento === value && x !== w);
                    if (!incomp[w]) incomp[w] = [];
                    miembros.forEach(m => {
                        if (!incomp[w].includes(m)) incomp[w].push(m);
                        if (!incomp[m]) incomp[m] = [];
                        if (!incomp[m].includes(w)) incomp[m].push(w);
                    });
                }
                
                // Heredar cierres de departamento
                if (cierres[value]) {
                    const newVacs = [...newTrabajadores[w].vacaciones];
                    cierres[value].forEach(f => {
                        if (!newVacs.includes(f)) newVacs.push(f);
                    });
                    newTrabajadores[w].vacaciones = newVacs;
                }
            }
            
            if (field === 'diasBase_sum' || field === 'diasExtras_sum') {
                const actualField = field === 'diasBase_sum' ? 'diasBase' : 'diasExtras';
                newTrabajadores[w][actualField] = Math.max(0, (newTrabajadores[w][actualField] || 0) + parseInt(value));
            } else {
                newTrabajadores[w][field] = value;
            }
        });
        setLocal({ ...local, trabajadores: newTrabajadores, incompatibilidades: incomp });
    };

    // Lista de trabajadores ordenada para selectores
    const sortedWorkers = Object.keys(local.trabajadores).sort();

    return html`
        <div className="modal d-block" style=${{ background: 'rgba(0,0,0,0.5)', overflowY: 'auto' }}>
            <div className="modal-dialog modal-lg"><div className="modal-content">
                <div className="modal-header bg-light">
                    <h5 className="modal-title"><i className="bi bi-sliders me-2"></i>Configuración</h5>
                    <button className="btn-close" onClick=${onClose}></button>
                </div>
                <div className="modal-body">
                    <!-- Pestañas del diálogo de configuración -->
                    <ul className="nav nav-tabs mb-3">
                        <li className="nav-item">
                            <button className=${`nav-link ${configTab === 'personal' ? 'active' : ''}`} onClick=${() => setConfigTab('personal')}>
                                <i className="bi bi-people me-1"></i>Personal
                            </button>
                        </li>
                        <li className="nav-item">
                            <button className=${`nav-link ${configTab === 'general' ? 'active' : ''}`} onClick=${() => setConfigTab('general')}>
                                <i className="bi bi-gear me-1"></i>General
                            </button>
                        </li>
                        <li className="nav-item">
                            <button className=${`nav-link ${configTab === 'departamentos' ? 'active' : ''}`} onClick=${() => setConfigTab('departamentos')}>
                                <i className="bi bi-building me-1"></i>Departamentos
                            </button>
                        </li>
                        <li className="nav-item">
                            <button className=${`nav-link ${configTab === 'incompatibilidades' ? 'active' : ''}`} onClick=${() => setConfigTab('incompatibilidades')}>
                                <i className="bi bi-exclamation-triangle me-1"></i>Incompatibilidades
                            </button>
                        </li>
                    </ul>

                    ${configTab === 'personal' ? html`
                    <!-- ===== PESTAÑA: PERSONAL ===== -->
                    <div>
                        <p className="text-muted small mb-3">
                            <i className="bi bi-info-circle me-1"></i>
                            Gestión del personal y cupos de vacaciones. Puedes editar en lote seleccionando varios trabajadores.
                        </p>
                        
                        <div className="table-responsive mb-3" style=${{ maxHeight: '300px' }}>
                            <table className="table table-sm table-bordered table-hover align-middle">
                                <thead className="table-light sticky-top">
                                    <tr>
                                        <th className="text-center" style=${{ width: '40px' }}>
                                            <input type="checkbox" className="form-check-input" 
                                                checked=${selectedWorkers.length > 0 && selectedWorkers.length === sortedWorkers.length}
                                                onChange=${e => setSelectedWorkers(e.target.checked ? sortedWorkers : [])} />
                                        </th>
                                        <th>Nombre</th>
                                        <th>Departamento</th>
                                        <th>Días Base</th>
                                        <th>Extras</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    ${sortedWorkers.map(w => html`
                                        <tr key=${w}>
                                            <td className="text-center">
                                                <input type="checkbox" className="form-check-input" 
                                                    checked=${selectedWorkers.includes(w)} 
                                                    onChange=${() => toggleWorkerSelection(w)} />
                                            </td>
                                            <td className="fw-bold">${w}</td>
                                            <td>${local.trabajadores[w].departamento || "General"}</td>
                                            <td>${local.trabajadores[w].diasBase}</td>
                                            <td>${local.trabajadores[w].diasExtras}</td>
                                        </tr>
                                    `)}
                                </tbody>
                            </table>
                        </div>

                        <!-- Panel de Acciones en Lote -->
                        <div className="card bg-light border-primary border-opacity-25 mb-3">
                            <div className="card-body py-2">
                                <h6 className="card-title text-primary small fw-bold mb-2">✏️ Acciones en Lote (${selectedWorkers.length} seleccionados)</h6>
                                <div className="row g-2 align-items-end">
                                    <div className="col-auto">
                                        <label className="form-label small mb-1">Días Base</label>
                                        <input type="number" className="form-control form-control-sm" value=${batchBase} onChange=${e => setBatchBase(e.target.value)} style=${{ width: '70px' }} />
                                    </div>
                                    <div className="col-auto">
                                        <div className="btn-group btn-group-sm">
                                            <button className="btn btn-outline-primary" onClick=${() => applyBatchUpdate('diasBase', batchBase)} disabled=${selectedWorkers.length===0}>Asignar</button>
                                            <button className="btn btn-outline-success" onClick=${() => applyBatchUpdate('diasBase_sum', batchBase)} disabled=${selectedWorkers.length===0}>+ Sumar</button>
                                            <button className="btn btn-outline-danger" onClick=${() => applyBatchUpdate('diasBase_sum', -batchBase)} disabled=${selectedWorkers.length===0}>- Restar</button>
                                        </div>
                                    </div>
                                    <div className="col-auto ms-3">
                                        <label className="form-label small mb-1">Extras</label>
                                        <input type="number" className="form-control form-control-sm" value=${batchExtra} onChange=${e => setBatchExtra(e.target.value)} style=${{ width: '70px' }} />
                                    </div>
                                    <div className="col-auto">
                                        <div className="btn-group btn-group-sm">
                                            <button className="btn btn-outline-primary" onClick=${() => applyBatchUpdate('diasExtras', batchExtra)} disabled=${selectedWorkers.length===0}>Asignar</button>
                                            <button className="btn btn-outline-success" onClick=${() => applyBatchUpdate('diasExtras_sum', batchExtra)} disabled=${selectedWorkers.length===0}>+ Sumar</button>
                                            <button className="btn btn-outline-danger" onClick=${() => applyBatchUpdate('diasExtras_sum', -batchExtra)} disabled=${selectedWorkers.length===0}>- Restar</button>
                                        </div>
                                    </div>
                                    <div className="col-12 mt-2">
                                        <div className="d-flex gap-2 align-items-end">
                                            <div>
                                                <label className="form-label small mb-1">Departamento</label>
                                                <select className="form-select form-select-sm" value=${batchDept} onChange=${e => setBatchDept(e.target.value)}>
                                                    ${local.departamentos.map(d => html`<option key=${d} value=${d}>${d}</option>`)}
                                                </select>
                                            </div>
                                            <button className="btn btn-sm btn-outline-primary" onClick=${() => applyBatchUpdate('departamento', batchDept)} disabled=${selectedWorkers.length===0}>Asignar</button>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div className="d-flex gap-2">
                            <input className="form-control" placeholder="Nuevo trabajador..." value=${newWorkerName} onChange=${e => setNewWorkerName(e.target.value)} onKeyDown=${e => e.key === 'Enter' && addTrabajador()} />
                            <button className="btn btn-success text-nowrap" onClick=${addTrabajador}><i className="bi bi-plus-lg me-1"></i> Añadir</button>
                            <button className="btn btn-danger text-nowrap ms-2" onClick=${removeSelectedWorkers} disabled=${selectedWorkers.length===0}><i className="bi bi-trash"></i> Eliminar</button>
                        </div>
                    </div>
                    ` : null}

                    ${configTab === 'general' ? html`
                    <!-- ===== PESTAÑA: GENERAL ===== -->
                    <div className="row g-3">
                        <div className="col-md-6">
                            <label className="form-label fw-bold">Título del Documento</label>
                            <input className="form-control" value=${local.titulo_pagina} onChange=${e => setLocal({ ...local, titulo_pagina: e.target.value })}/>
                        </div>
                        <div className="col-md-6">
                            <label className="form-label fw-bold">Año de Cupo</label>
                            <input type="number" className="form-control" value=${local.year} onChange=${e => setLocal({ ...local, year: parseInt(e.target.value) })}/>
                        </div>
                        <div className="col-md-12">
                            <label className="form-label fw-bold">Comunidad Autónoma (OpenHolidays)</label>
                            <div className="d-flex gap-2">
                                <select className="form-select" value=${local.comunidadAutonoma} onChange=${e => setLocal({ ...local, comunidadAutonoma: e.target.value })}>
                                    ${COMUNIDADES.map(c => html`<option key=${c.id} value=${c.id}>${c.name}</option>`)}
                                </select>
                                <button className="btn btn-outline-primary" style=${{ whiteSpace: 'nowrap' }} onClick=${fetchHolidays}>Importar Festivos</button>
                            </div>
                            <div className="d-flex justify-content-between align-items-center mt-3 border-top pt-2">
                                <div className="text-muted small fw-bold">Añadir Festivo Manual:</div>
                                <div className="d-flex gap-2 align-items-center">
                                    <select className="form-select form-select-sm" value=${newHolidayDept} onChange=${e => setNewHolidayDept(e.target.value)}>
                                        <option value="Global">Todos (Global)</option>
                                        ${local.departamentos.map(d => html`<option key=${d} value=${d}>${d}</option>`)}
                                    </select>
                                    <input type="date" className="form-control form-control-sm" value=${newHoliday} onChange=${e => setNewHoliday(e.target.value)} />
                                    <button className="btn btn-sm btn-success" onClick=${addManualHoliday}><i className="bi bi-plus"></i></button>
                                </div>
                            </div>
                            <div className="d-flex justify-content-between align-items-center mt-2">
                                <div className="text-muted small fw-bold">Festivos cargados actualmente: ${(local.festivos?.length || 0) + Object.values(local.festivosDepartamento || {}).flat().length}</div>
                                <button className="btn btn-sm btn-outline-danger" onClick=${() => setLocal({ ...local, festivos: [], festivosDepartamento: {} })}>Borrar Todos</button>
                            </div>
                            <div className="d-flex flex-wrap gap-1 mt-2 p-2 border rounded bg-light" style=${{ maxHeight: '100px', overflowY: 'auto' }}>
                                ${local.festivos && local.festivos.length > 0 ?
                                    local.festivos.map(f => html`<span key=${"g_"+f} className="badge bg-secondary d-flex align-items-center gap-1">${f} (Global) <i className="bi bi-x-circle cursor-pointer" onClick=${() => removeHoliday(f, true, null)}></i></span>`)
                                    : null
                                }
                                ${local.festivosDepartamento ? 
                                    Object.keys(local.festivosDepartamento).flatMap(dept => 
                                        local.festivosDepartamento[dept].map(f => html`<span key=${dept+"_"+f} className="badge bg-info text-dark d-flex align-items-center gap-1">${f} (${dept}) <i className="bi bi-x-circle cursor-pointer" onClick=${() => removeHoliday(f, false, dept)}></i></span>`)
                                    ) : null
                                }
                                ${(!local.festivos || local.festivos.length === 0) && (!local.festivosDepartamento || Object.keys(local.festivosDepartamento).length === 0) ? html`<span className="text-muted small">No hay festivos cargados</span>` : null}
                            </div>
                        </div>
                        <div className="col-12"><hr/></div>
                        <div className="col-12"><h6 className="fw-bold">Ajustes de PDF y Vistas</h6></div>
                        
                        <div className="col-md-6">
                            <label className="form-label">Pie de Página (PDF)</label>
                            <input className="form-control" value=${local.pie_pagina_pdf} onChange=${e => setLocal({ ...local, pie_pagina_pdf: e.target.value })}/>
                        </div>
                        <div className="col-md-6">
                            <label className="form-label">Orientación (PDF Mensual)</label>
                            <select className="form-select" value=${local.orientacion_pdf} onChange=${e => setLocal({ ...local, orientacion_pdf: e.target.value })}>
                                <option value="Portrait">Vertical (Portrait)</option>
                                <option value="Landscape">Apaisado (Landscape)</option>
                            </select>
                        </div>
                        <div className="col-12">
                            <div className="form-check form-switch mb-2">
                                <input className="form-check-input" type="checkbox" checked=${local.ocultar_computo_gantt} onChange=${e => setLocal({ ...local, ocultar_computo_gantt: e.target.checked })} />
                                <label className="form-check-label">Ocultar cómputo total de días en PDF Gantt</label>
                            </div>
                            <div className="form-check form-switch mb-2">
                                <input className="form-check-input" type="checkbox" checked=${local.ocultar_meses_sin_dias} onChange=${e => setLocal({ ...local, ocultar_meses_sin_dias: e.target.checked })} />
                                <label className="form-check-label">Ocultar meses sin días asignados en PDF</label>
                            </div>
                            <div className="form-check form-switch">
                                <input className="form-check-input" type="checkbox" checked=${local.forzar_salto_pagina} onChange=${e => setLocal({ ...local, forzar_salto_pagina: e.target.checked })} />
                                <label className="form-check-label">Forzar salto de página antes del resumen en PDF</label>
                            </div>
                        </div>
                        <div className="col-12 mt-3">
                            <label className="form-label fw-bold">Meses a mostrar (Vista y PDF)</label>
                            <div className="d-flex flex-wrap gap-2">
                                ${NOMBRES_MESES.map((nm, idx) => html`
                                    <div key=${idx} className="form-check form-check-inline" style=${{ width: '100px' }}>
                                        <input className="form-check-input" type="checkbox" id=${"m" + idx} checked=${local.meses_a_mostrar.includes(idx + 1)} onChange=${() => toggleMes(idx + 1)} />
                                        <label className="form-check-label" htmlFor=${"m" + idx}>${nm}</label>
                                    </div>
                                `)}
                            </div>
                        </div>
                    </div>
                    ` : null}

                    ${configTab === 'departamentos' ? html`
                    <!-- ===== PESTAÑA: DEPARTAMENTOS ===== -->
                    <div>
                        <p className="text-muted small mb-3">
                            <i className="bi bi-info-circle me-1"></i>
                            Los departamentos se usan para agrupar trabajadores. Puedes aplicar cierres de empresa por departamento
                            y gestionar incompatibilidades de vacaciones entre miembros del mismo departamento.
                        </p>
                        <div className="d-flex gap-2 mb-3">
                            <input className="form-control" placeholder="Nuevo departamento..." value=${newDeptName} 
                                   onChange=${e => setNewDeptName(e.target.value)} 
                                   onKeyDown=${e => e.key === 'Enter' && addDepartamento()} />
                            <button className="btn btn-success" onClick=${addDepartamento}><i className="bi bi-plus-lg"></i></button>
                        </div>
                        <div className="list-group">
                            ${local.departamentos.map(dept => {
                                const count = Object.values(local.trabajadores).filter(w => (w.departamento || "General") === dept).length;
                                const colorDept = (local.departamentosColores && local.departamentosColores[dept]) || '#aed6f1';
                                return html`
                                    <div key=${dept} className="list-group-item d-flex justify-content-between align-items-center">
                                        <div className="d-flex align-items-center">
                                            <input type="color" className="form-control form-control-color form-control-sm me-2" 
                                                   value=${colorDept} 
                                                   onChange=${e => {
                                                       const newColor = e.target.value;
                                                       setLocal(prev => {
                                                           const next = {...prev};
                                                           if (!next.departamentosColores) next.departamentosColores = {};
                                                           next.departamentosColores[dept] = newColor;
                                                           return next;
                                                       });
                                                   }} 
                                                   title="Color del departamento" />
                                            <strong>${dept}</strong>
                                            <span className="badge bg-secondary ms-2">${count} trabajadores</span>
                                        </div>
                                        ${dept !== "General" ? html`
                                            <button className="btn btn-sm btn-outline-danger" onClick=${() => removeDepartamento(dept)} title="Eliminar departamento">
                                                <i className="bi bi-trash"></i>
                                            </button>
                                        ` : html`<span className="badge bg-info">Por defecto</span>`}
                                    </div>
                                `;
                            })}
                        </div>
                    </div>
                    ` : null}

                    ${configTab === 'incompatibilidades' ? html`
                    <!-- ===== PESTAÑA: INCOMPATIBILIDADES ===== -->
                    <div>
                        <p className="text-muted small mb-3">
                            <i className="bi bi-exclamation-triangle me-1 text-warning"></i>
                            Las incompatibilidades de vacaciones generan <strong>avisos (no bloqueantes)</strong> cuando dos trabajadores
                            incompatibles coinciden en el mismo día de vacaciones. Similar al aviso de cupo superado.
                        </p>
                        
                        <!-- Sección: Añadir incompatibilidad individual -->
                        <div className="card border-0 bg-light mb-3">
                            <div className="card-body">
                                <h6 className="fw-bold mb-2"><i className="bi bi-person me-1"></i>Incompatibilidad Individual</h6>
                                <div className="d-flex gap-2 align-items-end flex-wrap">
                                    <div>
                                        <label className="form-label small">Trabajador</label>
                                        <select className="form-select form-select-sm" value=${selectedIncompWorker} 
                                                onChange=${e => setSelectedIncompWorker(e.target.value)}>
                                            <option value="">Seleccionar...</option>
                                            ${sortedWorkers.map(w => html`<option key=${w} value=${w}>${w}</option>`)}
                                        </select>
                                    </div>
                                    <div className="text-muted fw-bold pb-1">⟷</div>
                                    <div>
                                        <label className="form-label small">Incompatible con</label>
                                        <select className="form-select form-select-sm" value=${selectedIncompTarget}
                                                onChange=${e => setSelectedIncompTarget(e.target.value)}>
                                            <option value="">Seleccionar...</option>
                                            ${sortedWorkers.filter(w => w !== selectedIncompWorker).map(w => html`<option key=${w} value=${w}>${w}</option>`)}
                                        </select>
                                    </div>
                                    <button className="btn btn-sm btn-success" onClick=${addIncompatibilidadIndividual} 
                                            disabled=${!selectedIncompWorker || !selectedIncompTarget}>
                                        <i className="bi bi-plus-lg me-1"></i>Añadir
                                    </button>
                                </div>
                            </div>
                        </div>

                        <!-- Sección: Añadir incompatibilidades por departamento -->
                        <div className="card border-0 bg-light mb-3">
                            <div className="card-body">
                                <h6 className="fw-bold mb-2"><i className="bi bi-building me-1"></i>Incompatibilidad por Departamento</h6>
                                <p className="small text-muted mb-2">
                                    Marca a todos los miembros del departamento como incompatibles entre sí.
                                    Se podrán editar individualmente después.
                                </p>
                                <div className="d-flex gap-2 align-items-end">
                                    <div className="flex-grow-1">
                                        <select className="form-select form-select-sm" value=${selectedIncompDept}
                                                onChange=${e => setSelectedIncompDept(e.target.value)}>
                                            <option value="">Seleccionar departamento...</option>
                                            ${local.departamentos.map(d => html`<option key=${d} value=${d}>${d}</option>`)}
                                        </select>
                                    </div>
                                    <button className="btn btn-sm btn-warning" onClick=${addIncompatibilidadDepartamento} 
                                            disabled=${!selectedIncompDept}>
                                        <i className="bi bi-people me-1"></i>Aplicar
                                    </button>
                                </div>
                            </div>
                        </div>

                        <!-- Lista de incompatibilidades actuales -->
                        <h6 className="fw-bold mt-3"><i className="bi bi-list-check me-1"></i>Reglas activas</h6>
                        ${(() => {
                            // Generar pares únicos para no mostrar duplicados (A↔B solo una vez)
                            const pares = [];
                            const vistos = new Set();
                            Object.keys(local.incompatibilidades).forEach(worker => {
                                (local.incompatibilidades[worker] || []).forEach(target => {
                                    const key = [worker, target].sort().join("||");
                                    if (!vistos.has(key)) {
                                        vistos.add(key);
                                        pares.push({ a: worker, b: target });
                                    }
                                });
                            });
                            if (pares.length === 0) return html`<div className="text-muted small p-3 text-center border rounded">No hay incompatibilidades configuradas.</div>`;
                            return html`
                                <div className="list-group" style=${{ maxHeight: '200px', overflowY: 'auto' }}>
                                    ${pares.map(p => html`
                                        <div key=${p.a + '||' + p.b} className="list-group-item d-flex justify-content-between align-items-center py-1">
                                            <span>
                                                <i className="bi bi-person text-primary me-1"></i>${p.a}
                                                <span className="mx-2 text-warning fw-bold">⟷</span>
                                                <i className="bi bi-person text-primary me-1"></i>${p.b}
                                            </span>
                                            <button className="btn btn-sm btn-outline-danger py-0" onClick=${() => removeIncompatibilidad(p.a, p.b)}>
                                                <i className="bi bi-x"></i>
                                            </button>
                                        </div>
                                    `)}
                                </div>
                            `;
                        })()}
                    </div>
                    ` : null}

                </div>
                <div className="modal-footer bg-light d-flex justify-content-between">
                    <button className="btn btn-danger" onClick=${() => {
                        if (confirm("¿Estás seguro de que quieres borrar TODOS los datos (trabajadores, festivos y configuraciones)? Esto no se puede deshacer.")) {
                            onSave({ ...DEFAULT_CONFIG, year: local.year });
                        }
                    }}><i className="bi bi-trash3 me-1"></i> Borrar Todo</button>
                    <div>
                        <button className="btn btn-secondary me-2" onClick=${onClose}>Cancelar</button>
                        <button className="btn btn-primary" onClick=${() => onSave(local)}><i className="bi bi-save me-1"></i> Guardar Cambios</button>
                    </div>
                </div>
            </div></div>
        </div>
    `;
};

// ============================================================================
// COMPONENTE: CIERREEMPRESADIALOG (Diálogo de cierre de empresa)
// ============================================================================

/**
 * Diálogo modal para aplicar cierres de empresa o departamento en lote.
 * Permite seleccionar un rango de fechas y un departamento (o "Todos"),
 * y asigna automáticamente esos días como vacaciones a todos los trabajadores
 * del departamento seleccionado.
 * 
 * Los trabajadores pueden desmarcar individualmente las fechas asignadas en lote.
 * 
 * @param {Object} props
 * @param {boolean} props.show - Si true, muestra el diálogo.
 * @param {Object} props.data - Datos actuales de la aplicación.
 * @param {Function} props.onClose - Callback para cerrar el diálogo.
 * @param {Function} props.onApply - Callback que recibe los datos actualizados tras aplicar el cierre.
 */
const CierreEmpresaDialog = ({ show, data, onClose, onApply }) => {
    if (!show) return null;

    const [fechaInicio, setFechaInicio] = useState("");
    const [fechaFin, setFechaFin] = useState("");
    const [deptSeleccionado, setDeptSeleccionado] = useState("__todos__");
    const departamentos = data.departamentos || ["General"];

    /**
     * Aplica el cierre: asigna cada día laborable del rango seleccionado
     * como vacación a los trabajadores del departamento (o todos).
     * Omite fines de semana y festivos del cómputo.
     */
    const aplicarCierre = () => {
        if (!fechaInicio || !fechaFin) {
            alert("Selecciona fecha de inicio y fin.");
            return;
        }

        // Generar lista de fechas laborables en el rango
        const start = new Date(fechaInicio);
        const end = new Date(fechaFin);
        if (start > end) {
            alert("La fecha de inicio no puede ser posterior a la fecha de fin.");
            return;
        }

        const fechasLaborables = [];
        let current = new Date(start);
        while (current <= end) {
            const dayOfWeek = current.getDay();
            const dateStr = `${current.getDate().toString().padStart(2, '0')}/${(current.getMonth() + 1).toString().padStart(2, '0')}/${current.getFullYear()}`;
            // Solo días laborables que no sean festivos
            if (dayOfWeek !== 0 && dayOfWeek !== 6 && !data.festivos.includes(dateStr)) {
                fechasLaborables.push(dateStr);
            }
            current.setDate(current.getDate() + 1);
        }

        if (fechasLaborables.length === 0) {
            alert("No hay días laborables en el rango seleccionado.");
            return;
        }

        // Determinar trabajadores afectados
        const updatedTrabajadores = { ...data.trabajadores };
        let count = 0;
        Object.keys(updatedTrabajadores).forEach(name => {
            const worker = updatedTrabajadores[name];
            const deptWorker = worker.departamento || "General";
            if (deptSeleccionado === "__todos__" || deptWorker === deptSeleccionado) {
                const newVacs = [...worker.vacaciones];
                fechasLaborables.forEach(f => {
                    if (!newVacs.includes(f)) {
                        newVacs.push(f);
                    }
                });
                updatedTrabajadores[name] = { ...worker, vacaciones: newVacs };
                count++;
            }
        });

        const deptLabel = deptSeleccionado === "__todos__" ? "todos los departamentos" : `"${deptSeleccionado}"`;
        
        // Guardar las fechas de cierre en el modelo
        const updatedCierres = { ...(data.cierres || {}) };
        if (!updatedCierres[deptSeleccionado]) {
            updatedCierres[deptSeleccionado] = [];
        }
        fechasLaborables.forEach(f => {
            if (!updatedCierres[deptSeleccionado].includes(f)) {
                updatedCierres[deptSeleccionado].push(f);
            }
        });

        alert(`Cierre aplicado: ${fechasLaborables.length} días laborables asignados a ${count} trabajadores de ${deptLabel}.`);
        onApply({ ...data, trabajadores: updatedTrabajadores, cierres: updatedCierres });
    };

    return html`
        <div className="modal d-block" style=${{ background: 'rgba(0,0,0,0.5)', overflowY: 'auto' }}>
            <div className="modal-dialog"><div className="modal-content">
                <div className="modal-header bg-warning bg-opacity-25">
                    <h5 className="modal-title"><i className="bi bi-calendar-x me-2"></i>Cierre de Empresa / Departamento</h5>
                    <button className="btn-close" onClick=${onClose}></button>
                </div>
                <div className="modal-body">
                    <p className="text-muted small mb-3">
                        <i className="bi bi-info-circle me-1"></i>
                        Marca los días de cierre de la empresa o departamento. Se asignarán como vacaciones a todos los 
                        trabajadores del departamento seleccionado. Los trabajadores podrán desmarcar fechas individualmente después.
                    </p>
                    <div className="row g-3">
                        <div className="col-md-6">
                            <label className="form-label fw-bold">Fecha de Inicio</label>
                            <input type="date" className="form-control" value=${fechaInicio} onChange=${e => setFechaInicio(e.target.value)} />
                        </div>
                        <div className="col-md-6">
                            <label className="form-label fw-bold">Fecha de Fin</label>
                            <input type="date" className="form-control" value=${fechaFin} onChange=${e => setFechaFin(e.target.value)} />
                        </div>
                        <div className="col-12">
                            <label className="form-label fw-bold">Departamento</label>
                            <select className="form-select" value=${deptSeleccionado} onChange=${e => setDeptSeleccionado(e.target.value)}>
                                <option value="__todos__">Todos los departamentos</option>
                                ${departamentos.map(d => html`<option key=${d} value=${d}>${d}</option>`)}
                            </select>
                        </div>
                    </div>
                </div>
                <div className="modal-footer">
                    <button className="btn btn-secondary" onClick=${onClose}>Cancelar</button>
                    <button className="btn btn-warning" onClick=${aplicarCierre}><i className="bi bi-check-lg me-1"></i>Aplicar Cierre</button>
                </div>
            </div></div>
        </div>
    `;
};

// ============================================================================
// COMPONENTE: CALENDARGRID (Vista de calendario mensual)
// ============================================================================

/**
 * Vista de calendario mensual con interacción drag & drop.
 * Muestra los meses seleccionados como cuadrículas de 7 columnas (Lun-Dom).
 * Permite marcar/desmarcar días de vacaciones arrastrando sobre las celdas.
 * 
 * Colores de las celdas:
 * - Blanco: día laborable sin vacaciones
 * - Rojo claro: fin de semana
 * - Rojo intenso: festivo oficial
 * - Azul primario: vacación del trabajador activo
 * - Azul claro: vacación de otros trabajadores
 * 
 * @param {Object} props
 * @param {Object} props.data - Datos de la aplicación.
 * @param {string} props.activeWorker - Nombre del trabajador activo (o "" si ninguno).
 * @param {Function} props.onToggleDay - Callback(dateStr, mode) para marcar/desmarcar un día.
 */
const CalendarGrid = ({ data, activeWorker, onToggleDay, filtroDpto }) => {
    const meses = (data.meses_a_mostrar || [6, 7, 8, 9]).map(m => m - 1);
    const daysHeader = ["L", "M", "X", "J", "V", "S", "D"];

    // Estado del drag & drop para marcar múltiples días arrastrando
    const [isDragging, setIsDragging] = useState(false);
    const [dragMode, setDragMode] = useState(null); // 'add' o 'remove'

    /**
     * Inicia el arrastre al hacer clic en una celda de día.
     * Determina si el modo es 'add' (marcar) o 'remove' (desmarcar)
     * según si el día ya está marcado como vacación.
     */
    const handleMouseDown = (dateStr, isActiveWorkerOnVac) => {
        setIsDragging(true);
        const mode = isActiveWorkerOnVac ? 'remove' : 'add';
        setDragMode(mode);
        onToggleDay(dateStr, mode);
    };

    /**
     * Propaga la acción del arrastre a las celdas por las que pasa el ratón.
     */
    const handleMouseEnter = (dateStr) => {
        if (isDragging && dragMode) {
            onToggleDay(dateStr, dragMode);
        }
    };

    /** Finaliza el arrastre al soltar el botón del ratón. */
    const handleMouseUp = () => {
        setIsDragging(false);
        setDragMode(null);
    };

    // Listener global para detectar mouseup incluso fuera del calendario
    useEffect(() => {
        window.addEventListener('mouseup', handleMouseUp);
        return () => window.removeEventListener('mouseup', handleMouseUp);
    }, []);

    // Función auxiliar para colores
    const hexToRgb = (hex) => {
        const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        return result ? `${parseInt(result[1], 16)}, ${parseInt(result[2], 16)}, ${parseInt(result[3], 16)}` : '174, 214, 241';
    };

    return html`
        <div className="row g-4 mt-2" style=${{ userSelect: 'none' }}>
            ${meses.map(m => {
                const startOffset = (new Date(data.year, m, 1).getDay() || 7) - 1;
                const daysInMonth = new Date(data.year, m + 1, 0).getDate();
                const days = Array.from({ length: daysInMonth }, (_, i) => i + 1);
                const emptyCells = Array.from({ length: startOffset }, (_, i) => i);

                // Ocultar meses sin días asignados
                if (data.ocultar_meses_sin_dias) {
                    let hasDay = false;
                    Object.values(data.trabajadores).forEach(t => {
                        t.vacaciones.forEach(v => {
                            const [, vm, vy] = v.split("/");
                            if (parseInt(vm) === m + 1 && parseInt(vy) === data.year) hasDay = true;
                        });
                    });
                    if (!hasDay) return null;
                }

                return html`
                    <div key=${m} className="col-12 col-md-6 col-xl-4 col-xxl-3">
                        <div className="card h-100 shadow-sm border-0 bg-white">
                            <div className="card-header bg-transparent border-0 pt-3 pb-0">
                                <h5 className="fw-bold text-center text-primary mb-1" style=${{ textTransform: 'capitalize' }}>
                                    ${new Date(data.year, m).toLocaleString('es-ES', { month: 'long' })}
                                </h5>
                            </div>
                            <div className="card-body px-2 py-3">
                                <div className="d-grid" style=${{ gridTemplateColumns: 'repeat(7, 1fr)', gap: '4px' }}>
                                    ${daysHeader.map(d => html`<div key=${"header-" + d} className="text-center fw-bold text-muted small pb-1" style=${{ borderBottom: '2px solid #e2e8f0' }}>${d}</div>`)}
                                    ${emptyCells.map(i => html`<div key=${"empty-" + i} className="p-2"></div>`)}
                                    ${days.map(d => {
                                        const dateStr = `${String(d).padStart(2, '0')}/${String(m + 1).padStart(2, '0')}/${data.year}`;
                                        const dayOfWeek = new Date(data.year, m, d).getDay();
                                        const isWeekend = dayOfWeek === 0 || dayOfWeek === 6;
                                        const isHoliday = data.festivos.includes(dateStr) || 
                                                          (filtroDpto && data.festivosDepartamento && data.festivosDepartamento[filtroDpto] && data.festivosDepartamento[filtroDpto].includes(dateStr));

                                        let bgClass = "bg-white border";
                                        let textClass = "text-dark";
                                        let inlineStyle = { minHeight: '55px', display: 'flex', flexDirection: 'column' };
                                        let prefixText = "";

                                        // Identificar trabajadores de vacaciones este día
                                        let workersOnVac = Object.keys(data.trabajadores).filter(w => data.trabajadores[w].vacaciones.includes(dateStr));
                                        if (filtroDpto) {
                                            workersOnVac = workersOnVac.filter(w => (data.trabajadores[w].departamento || "General") === filtroDpto);
                                        }

                                        const isActiveWorkerOnVac = activeWorker && workersOnVac.includes(activeWorker);
                                        const cierresEnFecha = Object.keys(data.cierres || {}).filter(dept => data.cierres[dept].includes(dateStr));
                                        const isCierre = cierresEnFecha.length > 0 && (!filtroDpto || cierresEnFecha.includes(filtroDpto) || cierresEnFecha.includes("__todos__"));

                                        if (isHoliday) { 
                                            bgClass = "bg-danger opacity-75 border-danger"; textClass = "text-white fw-bold"; 
                                        } else if (isWeekend) { 
                                            bgClass = "bg-light border-light"; textClass = "text-muted"; 
                                        } else if (isCierre && workersOnVac.length === 0) {
                                            const closureDept = filtroDpto || cierresEnFecha[0];
                                            const deptColor = (data.departamentosColores && data.departamentosColores[closureDept]) || '#aed6f1';
                                            inlineStyle.backgroundColor = `rgba(${hexToRgb(deptColor)}, 0.4)`;
                                            bgClass = "border";
                                        }

                                        if (workersOnVac.length > 0 && !isHoliday && !isWeekend) {
                                            const primerWorker = workersOnVac[0];
                                            const wDept = data.trabajadores[primerWorker].departamento || "General";
                                            const deptColor = (data.departamentosColores && data.departamentosColores[wDept]) || '#aed6f1';
                                            
                                            // Chequear si es vacación de otro año
                                            const wInfo = data.trabajadores[primerWorker];
                                            const quotaYear = (wInfo.imputaciones && wInfo.imputaciones[dateStr]) || new Date(dateStr.split("/").reverse().join("-")).getFullYear();
                                            const isOtroAno = quotaYear !== data.year;

                                            if (isActiveWorkerOnVac) {
                                                if (isOtroAno) {
                                                    inlineStyle.backgroundColor = `rgba(${hexToRgb(deptColor)}, 0.7)`;
                                                    textClass = "text-dark fw-bold";
                                                } else {
                                                    inlineStyle.backgroundColor = deptColor;
                                                    textClass = "text-white fw-bold";
                                                }
                                                bgClass = "border";
                                            } else {
                                                if (isOtroAno) {
                                                    inlineStyle.backgroundColor = `rgba(${hexToRgb(deptColor)}, 0.3)`;
                                                } else {
                                                    inlineStyle.backgroundColor = `rgba(${hexToRgb(deptColor)}, 0.5)`;
                                                }
                                                bgClass = "border";
                                            }
                                            
                                            // Check incompatibilities
                                            const hasIncomp = workersOnVac.some(w => comprobarIncompatibilidades(w, dateStr, data.trabajadores, data.incompatibilidades || {}, data.cierres || {}).length > 0);
                                            if (hasIncomp) {
                                                textClass = "text-danger fw-bold";
                                                prefixText = "!";
                                            }
                                        }
                                        
                                        if (isCierre) {
                                            prefixText = "🔒" + prefixText;
                                        }

                                        return html`
                                            <div key=${d} 
                                                 className=${`p-1 rounded cursor-pointer day-cell ${bgClass} ${textClass}`}
                                                 style=${inlineStyle} 
                                                 onMouseDown=${() => handleMouseDown(dateStr, isActiveWorkerOnVac)}
                                                 onMouseEnter=${() => handleMouseEnter(dateStr)}
                                                 title=${workersOnVac.map(w => {
                                                     const inc = comprobarIncompatibilidades(w, dateStr, data.trabajadores, data.incompatibilidades || {}, data.cierres || {});
                                                     return inc.length > 0 ? `! ${w}` : w;
                                                 }).join('\n')}>
                                                <div className="text-end pe-1" style=${{ fontSize: '0.9rem' }}>${prefixText}${d}</div>
                                                <div className="mt-auto text-truncate-2" style=${{ fontSize: '0.65rem', lineHeight: 1.1, textAlign: 'left', paddingLeft: '2px' }}>
                                                    ${workersOnVac.slice(0, 2).map(w => html`<div key=${w}>${obtenerIniciales(w)}</div>`)}
                                                    ${workersOnVac.length > 2 ? html`<div>+${workersOnVac.length - 2}</div>` : null}
                                                </div>
                                            </div>
                                        `;
                                    })}
                                </div>
                            </div>
                        </div>
                    </div>
                `;
            })}
        </div>
    `;
};

// ============================================================================
// COMPONENTE: GANTTGRID (Vista Gantt / Tabla horizontal)
// ============================================================================

/**
 * Vista Gantt que muestra todos los trabajadores como filas
 * y los días del mes como columnas, con interacción drag & drop.
 * 
 * Cada celda se colorea según su estado:
 * - Azul: día de vacación del trabajador
 * - Rojo claro: festivo oficial
 * - Gris: fin de semana
 * - Blanco: día laborable disponible
 * 
 * A diferencia de CalendarGrid, permite seleccionar cualquier trabajador
 * directamente arrastrando en su fila, sin necesidad de seleccionarlo previamente.
 * 
 * @param {Object} props
 * @param {Object} props.data - Datos de la aplicación.
 * @param {string} props.activeWorker - Nombre del trabajador activo.
 * @param {Function} props.onToggleDay - Callback(dateStr, mode, workerName) para marcar/desmarcar.
 */
const GanttGrid = ({ data, activeWorker, onToggleDay, filtroDpto }) => {
    const meses = (data.meses_a_mostrar || [6, 7, 8, 9]).map(m => m - 1);

    // Estado del drag & drop específico de Gantt (incluye el trabajador arrastrado)
    const [isDragging, setIsDragging] = useState(false);
    const [dragMode, setDragMode] = useState(null);
    const [dragWorker, setDragWorker] = useState(null);

    const handleMouseDown = (dateStr, isVac, worker) => {
        if (activeWorker && activeWorker !== worker) return;
        setIsDragging(true);
        setDragWorker(worker);
        const mode = isVac ? 'remove' : 'add';
        setDragMode(mode);
        onToggleDay(dateStr, mode, worker);
    };

    const handleMouseEnter = (dateStr, worker) => {
        if (isDragging && dragMode && dragWorker === worker) {
            onToggleDay(dateStr, dragMode, worker);
        }
    };

    const handleMouseUp = () => {
        setIsDragging(false);
        setDragMode(null);
        setDragWorker(null);
    };

    useEffect(() => {
        window.addEventListener('mouseup', handleMouseUp);
        return () => window.removeEventListener('mouseup', handleMouseUp);
    }, []);

    // Función auxiliar para colores
    const hexToRgb = (hex) => {
        const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
        return result ? `${parseInt(result[1], 16)}, ${parseInt(result[2], 16)}, ${parseInt(result[3], 16)}` : '174, 214, 241';
    };

    return html`
        <div className="mt-3" style=${{ userSelect: 'none' }}>
            ${meses.map(m => {
                const totalDias = new Date(data.year, m + 1, 0).getDate();
                const days = Array.from({ length: totalDias }, (_, i) => i + 1);

                // Ocultar meses sin días asignados
                if (data.ocultar_meses_sin_dias) {
                    let hasDay = false;
                    Object.values(data.trabajadores).forEach(t => {
                        t.vacaciones.forEach(v => {
                            const [, vm, vy] = v.split("/");
                            if (parseInt(vm) === m + 1 && parseInt(vy) === data.year) hasDay = true;
                        });
                    });
                    if (!hasDay) return null;
                }

                let displayWorkers = Object.keys(data.trabajadores);
                if (filtroDpto) {
                    displayWorkers = displayWorkers.filter(w => (data.trabajadores[w].departamento || "General") === filtroDpto);
                }

                return html`
                    <div key=${m} className="card shadow-sm mb-4 border-0">
                        <div className="card-header bg-white fw-bold" style=${{ textTransform: 'capitalize' }}>
                            ${new Date(data.year, m).toLocaleString('es-ES', { month: 'long' })} ${data.year}
                        </div>
                        <div className="card-body p-0" style=${{ overflowX: 'auto' }}>
                            <table className="table table-bordered table-sm mb-0 text-center align-middle" style=${{ fontSize: '0.8rem' }}>
                                <thead className="table-light">
                                    <tr>
                                        <th style=${{ minWidth: '150px' }} className="text-start">TRABAJADOR</th>
                                        ${days.map(d => html`<th key=${d} style=${{ width: '25px', color: '#6c757d' }}>${d}</th>`)}
                                    </tr>
                                </thead>
                                <tbody>
                                    ${displayWorkers.sort().map(w => {
                                        const listVacaciones = data.trabajadores[w].vacaciones;
                                        const wDept = data.trabajadores[w].departamento || "General";
                                        const deptColor = (data.departamentosColores && data.departamentosColores[wDept]) || '#aed6f1';

                                        return html`
                                            <tr key=${w} className=${w === activeWorker ? "table-primary border-primary" : ""}>
                                                <td className="text-start fw-bold">${w}</td>
                                                ${days.map(d => {
                                                    const dateStr = `${String(d).padStart(2, '0')}/${String(m + 1).padStart(2, '0')}/${data.year}`;
                                                    const dayOfWeek = new Date(data.year, m, d).getDay();
                                                    const isWeekend = dayOfWeek === 0 || dayOfWeek === 6;
                                                    const festivosTrabajador = obtenerFestivosTrabajador(w, data);
                                                    const isHoliday = festivosTrabajador.includes(dateStr);
                                                    const isVacacion = listVacaciones.includes(dateStr);

                                                    const isCierre = data.cierres && ((data.cierres[wDept] && data.cierres[wDept].includes(dateStr)) || (data.cierres["__todos__"] && data.cierres["__todos__"].includes(dateStr)));
                                                    
                                                    const wInfo = data.trabajadores[w];
                                                    const quotaYear = (wInfo.imputaciones && wInfo.imputaciones[dateStr]) || new Date(dateStr.split("/").reverse().join("-")).getFullYear();
                                                    const isOtroAno = quotaYear !== data.year;

                                                    const incomp = comprobarIncompatibilidades(w, dateStr, data.trabajadores, data.incompatibilidades || {}, data.cierres || {});
                                                    const hasIncomp = isVacacion && incomp.length > 0;

                                                    let bg = ""; let icon = "";
                                                    let inlineStyle = {};

                                                    if (isVacacion) { 
                                                        if (isOtroAno) {
                                                            inlineStyle.backgroundColor = `rgba(${hexToRgb(deptColor)}, 0.7)`;
                                                        } else {
                                                            inlineStyle.backgroundColor = deptColor;
                                                        }
                                                        bg = "text-white"; 
                                                        if (hasIncomp) {
                                                            icon = "!";
                                                            bg += " fw-bold text-danger";
                                                            inlineStyle.color = "#dc3545";
                                                        } else if (isCierre) {
                                                            icon = "🔒";
                                                        } else {
                                                            icon = "";
                                                        }
                                                    }
                                                    else if (isHoliday) { bg = "bg-danger opacity-25 text-danger fw-bold"; icon = "F"; }
                                                    else if (isWeekend) { bg = "bg-secondary opacity-25 text-muted"; icon = ""; }

                                                    let cursor = (activeWorker === w || !activeWorker) && !isHoliday && !isWeekend ? "cursor-pointer" : "";

                                                    return html`
                                                        <td key=${d} 
                                                            className=${`${bg} ${cursor}`} 
                                                            style=${inlineStyle}
                                                            title=${dateStr + (hasIncomp ? ` Incompatible con: ${incomp.join(', ')}` : "")}
                                                            onMouseDown=${() => cursor ? handleMouseDown(dateStr, isVacacion, w) : null}
                                                            onMouseEnter=${() => cursor ? handleMouseEnter(dateStr, w) : null}
                                                        >
                                                            ${icon}
                                                        </td>
                                                    `;
                                                })}
                                            </tr>
                                        `;
                                    })}
                                </tbody>
                            </table>
                        </div>
                    </div>
                `;
            })}
        </div>
    `;
};

// ============================================================================
// COMPONENTE: RESUMENVACACIONES (Resumen anual con cómputo y alertas)
// ============================================================================

/**
 * Panel de resumen que muestra el cómputo anual de vacaciones por trabajador.
 * Incluye:
 * - Días consumidos vs. cupo disponible
 * - Alerta de "Cupo superado" cuando se exceden los días
 * - Texto descriptivo de los rangos de vacaciones
 * - Alertas de incompatibilidad cuando dos trabajadores incompatibles coinciden
 * 
 * @param {Object} props
 * @param {Object} props.data - Datos completos de la aplicación.
 */
const ResumenVacaciones = ({ data, filtroDpto }) => {
    let displayWorkers = Object.keys(data.trabajadores);
    if (filtroDpto) displayWorkers = displayWorkers.filter(w => (data.trabajadores[w].departamento || "General") === filtroDpto);

    return html`
        <div className="card mt-5 shadow-sm border-0">
            <div className="card-header bg-white fw-bold text-secondary">
                <i className="bi bi-card-list me-2"></i>Resumen Anual de Vacaciones (Leyenda)
            </div>
            <div className="card-body p-0">
                <ul className="list-group list-group-flush">
                    ${displayWorkers.sort().map(w => {
                        const festivosTrabajador = obtenerFestivosTrabajador(w, data);
                        const cons = contarDiasConsumidos(data.trabajadores[w].vacaciones, festivosTrabajador);
                        const limit = data.trabajadores[w].dias_base + data.trabajadores[w].dias_extras;
                        const wDept = data.trabajadores[w].departamento || "General";
                        const deptCierres = (data.cierres && data.cierres[wDept]) || [];
                        const generalCierres = (data.cierres && data.cierres["__todos__"]) || [];
                        
                        const vPropias = [];
                        const vCierres = [];
                        data.trabajadores[w].vacaciones.forEach(v => {
                            if (deptCierres.includes(v) || generalCierres.includes(v)) {
                                vCierres.push(v);
                            } else {
                                vPropias.push(v);
                            }
                        });

                        const txtPropias = vPropias.length > 0 ? agruparVacacionesEnTexto(vPropias, festivosTrabajador, data.year) : "Ninguna";
                        const txtCierres = vCierres.length > 0 ? agruparVacacionesEnTexto(vCierres, festivosTrabajador, data.year) : "";
                        const excede = cons > limit ? html`<span className="badge bg-danger ms-2">Cupo superado!</span>` : null;
                        
                        // Comprobar conflictos de incompatibilidad
                        const conflictos = obtenerTodosLosConflictos(w, data.trabajadores, data.incompatibilidades || {}, data.cierres || {});

                        return html`
                            <li key=${w} className="list-group-item">
                                <div className="fw-bold text-dark">
                                    [${obtenerIniciales(w)}] ${w}: <span className=${cons > limit ? "text-danger" : "text-primary"}>${cons} de ${limit} días consumidos</span>${excede}
                                </div>
                                <div className="text-muted small fst-italic mt-1">Vacaciones libres: ${txtPropias}</div>
                                ${txtCierres ? html`<div className="text-muted small fst-italic mt-1">🔒 Cierres patronales: ${txtCierres}</div>` : null}

                                ${conflictos.length > 0 ? html`
                                    <div className="mt-1">
                                        ${conflictos.map(c => html`
                                            <div key=${c.fecha} className="d-flex align-items-center gap-1">
                                                <span className="badge bg-warning text-dark">
                                                    <i className="bi bi-exclamation-triangle me-1"></i>
                                                    ${c.fecha}: coincide con ${c.conflictos.join(", ")}
                                                </span>
                                            </div>
                                        `)}
                                    </div>
                                ` : null}
                            </li>
                        `;
                    })}
                    ${Object.keys(data.trabajadores).length === 0 ? html`<li className="list-group-item text-muted">No hay trabajadores registrados.</li>` : null}
                </ul>
            </div>
        </div>
    `;
};
