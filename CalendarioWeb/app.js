// app.js
const { useState, useEffect, useMemo } = React;
const { Header, ConfigDialog, WorkerDialog } = window.AppComponents;
const { StorageService, HolidayService, ValidationService, ExportService } = window;

const App = () => {
    const [config, setConfig] = useState(null);
    const [trabajadores, setTrabajadores] = useState([]);
    
    // UI State
    const [showConfig, setShowConfig] = useState(false);
    const [editingWorker, setEditingWorker] = useState(null);
    const [showWorkerDialog, setShowWorkerDialog] = useState(false);
    const [currentMonth, setCurrentMonth] = useState(new Date().getMonth());

    useEffect(() => {
        const data = StorageService.loadData();
        setConfig(data.config);
        setTrabajadores(data.trabajadores || []);
    }, []);

    // Save to localstorage whenever state changes
    useEffect(() => {
        if (config) {
            StorageService.saveData({ config, trabajadores });
        }
    }, [config, trabajadores]);

    if (!config) return <div>Cargando...</div>;

    // Handlers
    const handleSaveConfig = (newConfig) => {
        setConfig(newConfig);
        setShowConfig(false);
    };

    const handleSaveWorker = (worker) => {
        if (editingWorker) {
            setTrabajadores(trabajadores.map(t => t.id === worker.id ? worker : t));
        } else {
            setTrabajadores([...trabajadores, worker]);
        }
        setShowWorkerDialog(false);
        setEditingWorker(null);
    };

    const handleDeleteWorker = (id) => {
        if(confirm("¿Seguro que deseas eliminar este trabajador?")) {
            setTrabajadores(trabajadores.filter(t => t.id !== id));
        }
    }

    const handleImportJson = (e) => {
        const file = e.target.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = (e) => {
            try {
                const data = JSON.parse(e.target.result);
                if(data.config && data.trabajadores) {
                    setConfig(data.config);
                    setTrabajadores(data.trabajadores);
                    alert("Datos importados correctamente");
                } else {
                    alert("Formato de archivo incorrecto");
                }
            } catch (err) {
                alert("Error al leer el archivo JSON");
            }
        };
        reader.readAsText(file);
    };

    // Calendar Helper
    const daysInMonth = new Date(config.ano, currentMonth + 1, 0).getDate();
    const daysArray = Array.from({length: daysInMonth}, (_, i) => i + 1);

    const isHoliday = (day) => {
        const dateStr = `${config.ano}-${String(currentMonth + 1).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
        return config.festivos?.some(f => f.fecha === dateStr);
    };

    const isWeekend = (day) => {
        const date = new Date(config.ano, currentMonth, day);
        return date.getDay() === 0 || date.getDay() === 6;
    };

    return (
        <div>
            <Header 
                config={config} 
                onOpenConfig={() => setShowConfig(true)}
                onExportJson={() => StorageService.exportJson({ config, trabajadores })}
                onImportJson={handleImportJson}
                onExportExcel={() => ExportService.exportToExcel({ config, trabajadores })}
                onExportPdf={() => ExportService.exportToPdf({ config, trabajadores })}
            />

            <div className="container-fluid">
                <div className="row mb-3">
                    <div className="col d-flex gap-2 align-items-center">
                        <button className="btn btn-success" onClick={() => { setEditingWorker(null); setShowWorkerDialog(true); }}>
                            <i className="bi bi-person-plus"></i> Nuevo Trabajador
                        </button>

                        <select className="form-select w-auto ms-auto" value={currentMonth} onChange={(e) => setCurrentMonth(parseInt(e.target.value))}>
                            {['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'].map((m, i) => (
                                <option key={i} value={i}>{m}</option>
                            ))}
                        </select>
                    </div>
                </div>

                <div className="card shadow-sm">
                    <div className="card-body p-0">
                        {/* Calendar Grid */}
                        <div className="calendar-grid" style={{ gridTemplateColumns: `250px repeat(${daysInMonth}, minmax(30px, 1fr))` }}>
                            {/* Header Row */}
                            <div className="calendar-header-cell worker-name-cell">Trabajador</div>
                            {daysArray.map(day => {
                                const we = isWeekend(day);
                                const hol = isHoliday(day);
                                let className = "calendar-header-cell";
                                if (hol) className += " holiday text-danger";
                                else if (we) className += " weekend";
                                
                                return (
                                    <div key={`h-${day}`} className={className} title={hol ? "Festivo" : ""}>
                                        {day}
                                    </div>
                                );
                            })}

                            {/* Worker Rows */}
                            {trabajadores.length === 0 ? (
                                <div style={{gridColumn: `1 / span ${daysInMonth + 1}`, padding: '1rem', textAlign: 'center'}}>
                                    No hay trabajadores registrados.
                                </div>
                            ) : trabajadores.map(worker => (
                                <React.Fragment key={worker.id}>
                                    <div className="calendar-cell worker-name-cell d-flex justify-content-between align-items-center">
                                        <div>
                                            <div className="fw-bold">{worker.nombre}</div>
                                            <div className="text-muted" style={{fontSize: '0.75rem'}}>{worker.departamento} ({worker.diasConsumidos}/{worker.diasTotales})</div>
                                        </div>
                                        <div>
                                            <button className="btn btn-sm btn-link text-primary p-0 me-2" onClick={() => { setEditingWorker(worker); setShowWorkerDialog(true); }}>
                                                <i className="bi bi-pencil"></i>
                                            </button>
                                            <button className="btn btn-sm btn-link text-danger p-0" onClick={() => handleDeleteWorker(worker.id)}>
                                                <i className="bi bi-trash"></i>
                                            </button>
                                        </div>
                                    </div>
                                    {daysArray.map(day => {
                                        // TODO: Implement actual vacation rendering logic here based on worker.vacaciones
                                        const we = isWeekend(day);
                                        const hol = isHoliday(day);
                                        let className = "calendar-cell";
                                        if (hol) className += " holiday";
                                        else if (we) className += " weekend";

                                        return <div key={`c-${worker.id}-${day}`} className={className}></div>;
                                    })}
                                </React.Fragment>
                            ))}
                        </div>
                    </div>
                </div>
            </div>

            <ConfigDialog 
                show={showConfig} 
                config={config} 
                onClose={() => setShowConfig(false)} 
                onSave={handleSaveConfig} 
            />

            <WorkerDialog 
                show={showWorkerDialog} 
                worker={editingWorker} 
                onClose={() => setShowWorkerDialog(false)} 
                onSave={handleSaveWorker} 
            />
        </div>
    );
};

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(<App />);
