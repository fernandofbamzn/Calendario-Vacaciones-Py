// components.js
const { useState, useEffect, useMemo } = React;

// --- EXPORT SERVICE ---
class ExportService {
    static exportToExcel(data) {
        // Implementación básica de exportación a Excel
        const wsData = [["Nombre", "Departamento", "Dias Consumidos", "Dias Totales"]];
        data.trabajadores.forEach(t => {
            wsData.push([t.nombre, t.departamento, t.diasConsumidos, t.diasTotales]);
        });
        const ws = window.XLSX.utils.aoa_to_sheet(wsData);
        const wb = window.XLSX.utils.book_new();
        window.XLSX.utils.book_append_sheet(wb, ws, "Trabajadores");
        window.XLSX.writeFile(wb, `Vacaciones_${data.config.ano}.xlsx`);
    }

    static exportToPdf(data) {
        // Implementación básica de jspdf-autotable
        const doc = new window.jspdf.jsPDF();
        doc.text(`Informe de Vacaciones - ${data.config.ano}`, 14, 15);
        
        const tableData = data.trabajadores.map(t => [
            t.nombre, t.departamento, t.diasConsumidos.toString(), t.diasTotales.toString()
        ]);

        doc.autoTable({
            head: [['Nombre', 'Departamento', 'Consumidos', 'Totales']],
            body: tableData,
            startY: 20
        });

        doc.save(`Vacaciones_${data.config.ano}.pdf`);
    }
}
window.ExportService = ExportService;

// --- COMPONENTS ---

const Header = ({ config, onExportJson, onImportJson, onExportExcel, onExportPdf, onOpenConfig }) => {
    return (
        <nav className="navbar navbar-expand-lg navbar-dark bg-primary mb-4">
            <div className="container-fluid">
                <span className="navbar-brand">
                    <i className="bi bi-calendar-check me-2"></i>
                    Gestor de Vacaciones {config.ano}
                </span>
                
                <div className="d-flex gap-2">
                    <button className="btn btn-light btn-sm" onClick={onOpenConfig}>
                        <i className="bi bi-gear"></i> Configuración
                    </button>
                    
                    <div className="dropdown">
                        <button className="btn btn-light btn-sm dropdown-toggle" type="button" data-bs-toggle="dropdown">
                            <i className="bi bi-cloud-arrow-down"></i> Exportar
                        </button>
                        <ul className="dropdown-menu">
                            <li><button className="dropdown-item" onClick={onExportJson}>JSON (Backup)</button></li>
                            <li><button className="dropdown-item" onClick={onExportExcel}>Excel</button></li>
                            <li><button className="dropdown-item" onClick={onExportPdf}>PDF</button></li>
                        </ul>
                    </div>

                    <label className="btn btn-light btn-sm mb-0">
                        <i className="bi bi-cloud-arrow-up"></i> Importar JSON
                        <input type="file" accept=".json" style={{display: 'none'}} onChange={onImportJson} />
                    </label>
                </div>
            </div>
        </nav>
    );
};

const ConfigDialog = ({ show, config, onClose, onSave }) => {
    if (!show) return null;

    const [localConfig, setLocalConfig] = useState({...config});
    const [loadingHolidays, setLoadingHolidays] = useState(false);

    const handleFetchHolidays = async () => {
        setLoadingHolidays(true);
        const festivos = await window.HolidayService.fetchHolidays('ES', localConfig.comunidadAutonoma, localConfig.ano);
        setLocalConfig(prev => ({ ...prev, festivos }));
        setLoadingHolidays(false);
    };

    return (
        <div className="modal d-block" style={{backgroundColor: 'rgba(0,0,0,0.5)'}}>
            <div className="modal-dialog">
                <div className="modal-content">
                    <div className="modal-header">
                        <h5 className="modal-title">Configuración</h5>
                        <button type="button" className="btn-close" onClick={onClose}></button>
                    </div>
                    <div className="modal-body">
                        <div className="mb-3">
                            <label className="form-label">Año</label>
                            <input type="number" className="form-control" 
                                value={localConfig.ano} 
                                onChange={e => setLocalConfig({...localConfig, ano: parseInt(e.target.value)})} 
                            />
                        </div>
                        <div className="mb-3">
                            <label className="form-label">Comunidad Autónoma</label>
                            <select className="form-select" 
                                value={localConfig.comunidadAutonoma}
                                onChange={e => setLocalConfig({...localConfig, comunidadAutonoma: e.target.value})}
                            >
                                <option value="ES-MD">Madrid</option>
                                <option value="ES-AN">Andalucía</option>
                                <option value="ES-CT">Cataluña</option>
                                {/* ... add more ... */}
                            </select>
                        </div>
                        <div className="mb-3 d-flex justify-content-between align-items-center">
                            <span>Festivos cargados: {localConfig.festivos?.length || 0}</span>
                            <button className="btn btn-secondary btn-sm" onClick={handleFetchHolidays} disabled={loadingHolidays}>
                                {loadingHolidays ? 'Cargando...' : 'Obtener Festivos OpenHolidays'}
                            </button>
                        </div>
                    </div>
                    <div className="modal-footer">
                        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
                        <button className="btn btn-primary" onClick={() => onSave(localConfig)}>Guardar</button>
                    </div>
                </div>
            </div>
        </div>
    );
};

const WorkerDialog = ({ show, worker, onClose, onSave }) => {
    if (!show) return null;

    const [localWorker, setLocalWorker] = useState(worker || { 
        id: Date.now().toString(), 
        nombre: '', 
        departamento: '', 
        diasTotales: 22, 
        diasConsumidos: 0, 
        vacaciones: [] 
    });

    return (
        <div className="modal d-block" style={{backgroundColor: 'rgba(0,0,0,0.5)'}}>
            <div className="modal-dialog">
                <div className="modal-content">
                    <div className="modal-header">
                        <h5 className="modal-title">{worker ? 'Editar Trabajador' : 'Nuevo Trabajador'}</h5>
                        <button type="button" className="btn-close" onClick={onClose}></button>
                    </div>
                    <div className="modal-body">
                        <div className="mb-3">
                            <label className="form-label">Nombre</label>
                            <input type="text" className="form-control" 
                                value={localWorker.nombre} 
                                onChange={e => setLocalWorker({...localWorker, nombre: e.target.value})} 
                            />
                        </div>
                        <div className="mb-3">
                            <label className="form-label">Departamento</label>
                            <input type="text" className="form-control" 
                                value={localWorker.departamento} 
                                onChange={e => setLocalWorker({...localWorker, departamento: e.target.value})} 
                            />
                        </div>
                        <div className="mb-3">
                            <label className="form-label">Días Totales</label>
                            <input type="number" className="form-control" 
                                value={localWorker.diasTotales} 
                                onChange={e => setLocalWorker({...localWorker, diasTotales: parseInt(e.target.value)})} 
                            />
                        </div>
                    </div>
                    <div className="modal-footer">
                        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
                        <button className="btn btn-primary" onClick={() => onSave(localWorker)}>Guardar</button>
                    </div>
                </div>
            </div>
        </div>
    );
};

window.AppComponents = { Header, ConfigDialog, WorkerDialog };
