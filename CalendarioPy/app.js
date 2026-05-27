// ==========================================
// ESTADO GLOBAL DE LA APLICACIÓN
// ==========================================
let trabajadores = {};
let festivos = [];
let currentYear = 2026;
let activeWorker = "";
let editMode = "vacaciones"; // "vacaciones" o "festivos"
let activeTab = "calendar"; // "calendar" o "gantt"

// Variables para control de arrastre (drag-to-select)
let isDragging = false;
let dragAction = null; // 'select' o 'deselect'
let dragSelectionType = null; // 'vacaciones' o 'festivos'

// ==========================================
// INICIALIZACIÓN
// ==========================================
let isInitialized = false;

document.addEventListener("DOMContentLoaded", () => {
    inicializarElementos();
    // Fallback por si no carga pywebview (modo web autónomo)
    setTimeout(async () => {
        if (!isDesktop() && !isInitialized) {
            isInitialized = true;
            await cargarDatos();
            actualizarSelectTrabajadores();
            actualizarPanelCupo();
            actualizarVistas();
        }
    }, 150);
});

window.addEventListener('pywebviewready', async () => {
    if (!isInitialized) {
        isInitialized = true;
        await cargarDatos();
        actualizarSelectTrabajadores();
        actualizarPanelCupo();
        actualizarVistas();
    }
});

function isDesktop() {
    return typeof window.pywebview !== "undefined" && typeof window.pywebview.api !== "undefined";
}

async function guardarArchivoDescarga(nombreSugerido, contenido, esBase64 = false) {
    if (isDesktop()) {
        try {
            const resultado = await window.pywebview.api.guardar_archivo(nombreSugerido, contenido, esBase64);
            if (resultado) {
                alert(`Archivo guardado con éxito en:\n${resultado}`);
            }
        } catch (e) {
            console.error("Error al guardar archivo nativo mediante pywebview:", e);
            alert("Error al guardar el archivo en la aplicación nativa.");
        }
    } else {
        let mimeType = "application/octet-stream";
        const ext = nombreSugerido.split('.').pop().toLowerCase();
        if (ext === "json") mimeType = "application/json";
        else if (ext === "csv") mimeType = "text/csv";
        else if (ext === "xls") mimeType = "application/vnd.ms-excel";
        else if (ext === "pdf") mimeType = "application/pdf";

        let url;
        if (esBase64) {
            url = contenido.startsWith("data:") ? contenido : `data:${mimeType};base64,${contenido}`;
        } else {
            const blob = new Blob([contenido], { type: mimeType + ";charset=utf-8;" });
            url = URL.createObjectURL(blob);
        }

        const link = document.createElement("a");
        link.setAttribute("href", url);
        link.setAttribute("download", nombreSugerido);
        link.style.visibility = "hidden";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
}

function esNombreValidoTrabajador(nombre) {
    if (!nombre) return false;
    const n = nombre.trim().toUpperCase();
    if (n === "" || n === "MES" || n === "TRABAJADOR" || n === "DIAS_BASE" || n === "DIAS_EXTRAS" || n === "FESTIVO" || n === "FESTIVOS") {
        return false;
    }
    // Ignorar si consiste puramente de dígitos
    if (/^\d+$/.test(n)) {
        return false;
    }
    return true;
}


// Cargar desde la API de Python o localStorage
async function cargarDatos() {
    if (isDesktop()) {
        try {
            const dataStr = await window.pywebview.api.cargar_datos_locales();
            const data = JSON.parse(dataStr);
            desempaquetarDatos(data);
        } catch (e) {
            console.error("Error al cargar datos locales desde pywebview:", e);
            cargarDesdeLocalStorage();
        }
    } else {
        cargarDesdeLocalStorage();
    }
}

function cargarDesdeLocalStorage() {
    const dataStr = localStorage.getItem("datos_vacaciones");
    if (dataStr) {
        try {
            const data = JSON.parse(dataStr);
            desempaquetarDatos(data);
        } catch (e) {
            console.error("Error al parsear localStorage:", e);
            inicializarDatosVacios();
        }
    } else {
        inicializarDatosVacios();
    }
}

function desempaquetarDatos(data) {
    trabajadores = data.trabajadores || {};
    festivos = data.festivos || [];
    currentYear = data.year || 2026;
    
    const titleInput = document.getElementById("page-title-input");
    if (titleInput) {
        titleInput.value = data.titulo_pagina || "Planificación de Vacaciones";
    }
    document.getElementById("label-year").textContent = currentYear;
}

function inicializarDatosVacios() {
    trabajadores = {};
    festivos = [];
    currentYear = 2026;
    const titleInput = document.getElementById("page-title-input");
    if (titleInput) {
        titleInput.value = "Planificación de Vacaciones";
    }
    document.getElementById("label-year").textContent = currentYear;
}

// Guardar en la API de Python o localStorage
async function guardarDatos() {
    const titleInput = document.getElementById("page-title-input");
    const data = {
        titulo_pagina: titleInput ? titleInput.value.trim() : "Planificación de Vacaciones",
        year: currentYear,
        festivos: festivos,
        trabajadores: trabajadores
    };
    
    if (isDesktop()) {
        try {
            await window.pywebview.api.guardar_datos_locales(JSON.stringify(data, null, 4));
        } catch (e) {
            console.error("Error al guardar datos locales mediante pywebview:", e);
        }
    } else {
        localStorage.setItem("datos_vacaciones", JSON.stringify(data));
    }
}

// ==========================================
// VINCULACIÓN DE EVENTOS DE INTERFAZ
// ==========================================
function inicializarElementos() {
    // Configurar año
    document.getElementById("label-year").textContent = currentYear;

    document.getElementById("btn-prev-year").addEventListener("click", async () => {
        currentYear--;
        document.getElementById("label-year").textContent = currentYear;
        await guardarDatos();
        actualizarVistas();
        actualizarPanelCupo();
    });

    document.getElementById("btn-next-year").addEventListener("click", async () => {
        currentYear++;
        document.getElementById("label-year").textContent = currentYear;
        await guardarDatos();
        actualizarVistas();
        actualizarPanelCupo();
    });

    // Título editable
    const titleInput = document.getElementById("page-title-input");
    if (titleInput) {
        titleInput.addEventListener("change", async () => {
            await guardarDatos();
        });
    }

    // Gestión de trabajadores
    document.getElementById("btn-add-worker").addEventListener("click", añadirTrabajador);
    document.getElementById("btn-delete-worker").addEventListener("click", eliminarTrabajadorActivo);
    document.getElementById("new-worker-name").addEventListener("keypress", (e) => {
        if (e.key === "Enter") añadirTrabajador();
    });

    document.getElementById("select-worker").addEventListener("change", (e) => {
        activeWorker = e.target.value;
        actualizarPanelCupo();
        actualizarVistas();
    });

    // Cambios en inputs de cupos
    document.getElementById("days-base").addEventListener("change", actualizarCupoTrabajador);
    document.getElementById("days-extras").addEventListener("change", actualizarCupoTrabajador);

    // Modos de edición
    const radioModes = document.querySelectorAll('input[name="edit-mode"]');
    radioModes.forEach(radio => {
        radio.addEventListener("change", (e) => {
            editMode = e.target.value;
            actualizarEstadoControles();
            actualizarVistas();
        });
    });

    // Navegación de pestañas
    document.getElementById("tab-calendar").addEventListener("click", () => switchTab("calendar"));
    document.getElementById("tab-gantt").addEventListener("click", () => switchTab("gantt"));

    // Descargas de PDF locales en el navegador
    document.getElementById("btn-pdf-mensual").addEventListener("click", generarPdfMensual);
    document.getElementById("btn-pdf-gantt").addEventListener("click", generarPdfGantt);

    // Botón Limpiar Todo local
    document.getElementById("btn-limpiar-datos").addEventListener("click", async () => {
        if (confirm("¿Estás seguro de que deseas limpiar todas las vacaciones y festivos del sistema? (Se mantendrán los trabajadores)")) {
            festivos = [];
            Object.keys(trabajadores).forEach(t => {
                trabajadores[t].vacaciones = [];
            });
            await guardarDatos();
            actualizarPanelCupo();
            actualizarVistas();
            alert("Todo el calendario ha sido limpiado.");
        }
    });

    // Configuración del importador unificado único
    document.getElementById("btn-import-unificado").addEventListener("click", () => {
        document.getElementById("import-file-unificado").click();
    });

    document.getElementById("import-file-unificado").addEventListener("change", async (e) => {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = async (event) => {
            const text = event.target.result;
            try {
                const es_json = file.name.endsWith(".json");
                const res = importarDesdeTexto(text, es_json);
                
                await guardarDatos();
                actualizarSelectTrabajadores();
                actualizarPanelCupo();
                actualizarVistas();
                
                alert(`Importación exitosa!\nTipo de Datos: ${res.tipo}\n\n${res.msg}`);
            } catch (err) {
                alert(`Error al procesar archivo: ${err.message}`);
            }
            e.target.value = ""; // Limpiar input file
        };
        reader.readAsText(file, "UTF-8");
    });
    
    // Configuración del modal de ayuda
    const modal = document.getElementById("modal-help");
    document.getElementById("btn-ayuda").addEventListener("click", () => modal.style.display = "block");
    document.querySelector(".close-modal").addEventListener("click", () => modal.style.display = "none");
    window.addEventListener("click", (e) => {
        if (e.target === modal) modal.style.display = "none";
    });

    // Eventos globales del ratón para finalizar el arrastre
    document.addEventListener("mouseup", async () => {
        if (isDragging) {
            isDragging = false;
            dragAction = null;
            dragSelectionType = null;
            await guardarDatos();
            actualizarVistas();
            actualizarPanelCupo();
        }
    });

    // Cargar nombres de trabajadores en el select
    actualizarSelectTrabajadores();
    actualizarEstadoControles();
}

// Cambiar de pestaña activa
function switchTab(tabName) {
    activeTab = tabName;
    document.getElementById("tab-calendar").classList.toggle("active", tabName === "calendar");
    document.getElementById("tab-gantt").classList.toggle("active", tabName === "gantt");

    document.getElementById("view-calendar-container").classList.toggle("active", tabName === "calendar");
    document.getElementById("view-gantt-container").classList.toggle("active", tabName === "gantt");

    actualizarVistas();
}

// Habilitar/Deshabilitar según el modo de edición
function actualizarEstadoControles() {
    const isFestivos = (editMode === "festivos");
    const quotaCard = document.getElementById("quota-card");
    const selectWorker = document.getElementById("select-worker");

    if (isFestivos) {
        quotaCard.classList.add("disabled");
        selectWorker.disabled = true;
    } else {
        quotaCard.classList.remove("disabled");
        selectWorker.disabled = false;
        actualizarPanelCupo();
    }
}

// ==========================================
// GESTIÓN DE TRABAJADORES Y CUPOS
// ==========================================
async function añadirTrabajador() {
    const input = document.getElementById("new-worker-name");
    const nombre = input.value.trim();

    if (!nombre) {
        alert("El nombre del trabajador no puede estar vacío.");
        return;
    }

    if (nombre.toUpperCase() === "FESTIVO") {
        alert("Nombre no permitido.");
        return;
    }

    if (trabajadores[nombre]) {
        alert(`El trabajador "${nombre}" ya existe.`);
        return;
    }

    // Registrar el trabajador localmente en el estado
    trabajadores[nombre] = {
        vacaciones: [],
        dias_base: 22,
        dias_extras: 0
    };

    input.value = "";
    await guardarDatos();
    actualizarSelectTrabajadores();
    
    // Seleccionar el nuevo trabajador
    document.getElementById("select-worker").value = nombre;
    activeWorker = nombre;
    
    actualizarPanelCupo();
    actualizarVistas();
    alert(`Trabajador "${nombre}" añadido con éxito.`);
}

async function eliminarTrabajadorActivo() {
    if (!activeWorker || !trabajadores[activeWorker]) {
        alert("Por favor, selecciona un trabajador activo para poder eliminarlo.");
        return;
    }

    const confirmar = confirm(`¿Estás seguro de que deseas eliminar al trabajador "${activeWorker}"?\nSe borrarán permanentemente todos sus registros de vacaciones.`);
    if (!confirmar) return;

    const nombreEliminado = activeWorker;
    delete trabajadores[activeWorker];
    activeWorker = ""; // Se actualizará en actualizarSelectTrabajadores
    
    await guardarDatos();
    actualizarSelectTrabajadores();
    actualizarPanelCupo();
    actualizarVistas();
    
    alert(`Trabajador "${nombreEliminado}" eliminado con éxito.`);
}

function actualizarSelectTrabajadores() {
    const select = document.getElementById("select-worker");
    select.innerHTML = "";

    const nombres = Object.keys(trabajadores).sort();
    if (nombres.length === 0) {
        const opt = document.createElement("option");
        opt.value = "";
        opt.disabled = true;
        opt.selected = true;
        opt.textContent = "Cargue o añada personal...";
        select.appendChild(opt);
        activeWorker = "";
        return;
    }

    nombres.forEach(nombre => {
        const opt = document.createElement("option");
        opt.value = nombre;
        opt.textContent = nombre;
        select.appendChild(opt);
    });

    // Mantener la selección anterior si sigue existiendo
    if (activeWorker && trabajadores[activeWorker]) {
        select.value = activeWorker;
    } else {
        select.value = nombres[0];
        activeWorker = nombres[0];
    }
}

async function actualizarCupoTrabajador() {
    if (!activeWorker || !trabajadores[activeWorker]) return;

    const base = parseInt(document.getElementById("days-base").value) || 0;
    const extras = parseInt(document.getElementById("days-extras").value) || 0;

    trabajadores[activeWorker].dias_base = base;
    trabajadores[activeWorker].dias_extras = extras;

    await guardarDatos();
    actualizarPanelCupo();
    actualizarResumen();
}

function actualizarPanelCupo() {
    if (!activeWorker || !trabajadores[activeWorker]) {
        document.getElementById("days-base").value = 22;
        document.getElementById("days-extras").value = 0;
        document.getElementById("progress-bar-fill").style.width = "0%";
        document.getElementById("label-quota-summary").textContent = "Sin trabajador activo";
        document.getElementById("label-quota-summary").className = "quota-summary";
        return;
    }

    const info = trabajadores[activeWorker];
    document.getElementById("days-base").value = info.dias_base;
    document.getElementById("days-extras").value = info.dias_extras;

    const totalDisponibles = info.dias_base + info.dias_extras;
    const consumidos = contarDiasConsumidos(activeWorker);
    const restantes = totalDisponibles - consumidos;

    let pct = totalDisponibles > 0 ? (consumidos / totalDisponibles) * 100 : 0;
    pct = Math.min(100, Math.max(0, pct));

    const fill = document.getElementById("progress-bar-fill");
    const summary = document.getElementById("label-quota-summary");

    fill.style.width = pct + "%";
    summary.textContent = `Consumidos: ${consumidos} / ${totalDisponibles} (Quedan: ${restantes})`;

    if (restantes < 0) {
        fill.className = "progress-bar-fill exceeded";
        summary.className = "quota-summary exceeded";
    } else {
        fill.className = "progress-bar-fill";
        summary.className = "quota-summary";
    }
}

// Contar días laborables netos (excluye fines de semana y festivos)
function contarDiasConsumidos(workerName) {
    if (!trabajadores[workerName]) return 0;
    
    let dias = 0;
    trabajadores[workerName].vacaciones.forEach(dateStr => {
        const parts = dateStr.split("/");
        const date = new Date(parseInt(parts[2]), parseInt(parts[1]) - 1, parseInt(parts[0]));
        const dayOfWeek = date.getDay(); // 0 es Domingo, 6 es Sábado
        
        const esFinSemana = (dayOfWeek === 0 || dayOfWeek === 6);
        const esFestivo = festivos.includes(dateStr);

        if (!esFinSemana && !esFestivo) {
            dias++;
        }
    });
    
    return dias;
}

// Agrupar vacaciones del trabajador en formato texto legible omitiendo fines de semana o festivos
function agruparVacacionesEnTexto(fechas, festivos, year) {
    if (!fechas || fechas.length === 0) return "Sin vacaciones asignadas";
    
    // Parsear fechas en formato DD/MM/YYYY y ordenarlas cronológicamente
    const list = fechas.map(f => {
        const parts = f.split("/");
        return {
            str: f,
            date: new Date(parseInt(parts[2]), parseInt(parts[1]) - 1, parseInt(parts[0]))
        };
    }).sort((a, b) => a.date - b.date);
    
    const ranges = [];
    let currentRange = [list[0]];
    
    for (let i = 1; i < list.length; i++) {
        const prevItem = currentRange[currentRange.length - 1];
        const currItem = list[i];
        
        let esContinuo = true;
        // Creamos una fecha temporal para iterar entre el día posterior al anterior y el día actual
        let tempDate = new Date(prevItem.date);
        tempDate.setDate(tempDate.getDate() + 1);
        
        while (tempDate < currItem.date) {
            const tempStr = `${tempDate.getDate().toString().padStart(2, '0')}/${(tempDate.getMonth() + 1).toString().padStart(2, '0')}/${tempDate.getFullYear()}`;
            const dayOfWeek = tempDate.getDay();
            const esFinSemana = (dayOfWeek === 0 || dayOfWeek === 6);
            const esFestivo = festivos.includes(tempStr);
            
            if (!esFinSemana && !esFestivo) {
                // Hay un día laborable no festivo de por medio, por tanto se interrumpe
                esContinuo = false;
                break;
            }
            tempDate.setDate(tempDate.getDate() + 1);
        }
        
        if (esContinuo) {
            currentRange.push(currItem);
        } else {
            ranges.push(currentRange);
            currentRange = [currItem];
        }
    }
    ranges.push(currentRange);
    
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    
    const rangesText = ranges.map(range => {
        const start = range[0].date;
        const end = range[range.length - 1].date;
        
        const startDay = start.getDate();
        const startMonth = nombresMeses[start.getMonth()];
        const endDay = end.getDate();
        const endMonth = nombresMeses[end.getMonth()];
        
        if (start.getTime() === end.getTime()) {
            return `el ${startDay} de ${startMonth}`;
        } else {
            if (start.getMonth() === end.getMonth()) {
                return `del ${startDay} al ${endDay} de ${startMonth}`;
            } else {
                return `del ${startDay} de ${startMonth} al ${endDay} de ${endMonth}`;
            }
        }
    });
    
    if (rangesText.length === 1) return rangesText[0];
    const lastText = rangesText.pop();
    return rangesText.join(", ") + " y " + lastText;
}

// ==========================================
// RENDERIZADO GENERAL Y CALENDARIO
// ==========================================
function actualizarVistas() {
    actualizarResumen();
    
    if (activeTab === "calendar") {
        renderCalendar();
    } else {
        renderGantt();
    }
}

// Renderizar el Calendario Mensual (Junio a Septiembre)
function renderCalendar() {
    const monthsGrid = document.getElementById("months-grid");
    monthsGrid.innerHTML = "";

    const mesesPintar = [5, 6, 7, 8]; // 5 es Junio, 8 es Septiembre (0-indexado)
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    mesesPintar.forEach(mesIndex => {
        const monthContainer = document.createElement("div");
        monthContainer.className = "month-container";

        const title = document.createElement("div");
        title.className = "month-name";
        title.textContent = `${nombresMeses[mesIndex]} ${currentYear}`;
        monthContainer.appendChild(title);

        const daysHeader = document.createElement("div");
        daysHeader.className = "month-days-header";
        const inicialesDias = ["L", "M", "X", "J", "V", "S", "D"];
        inicialesDias.forEach(d => {
            const dayLabel = document.createElement("span");
            dayLabel.textContent = d;
            daysHeader.appendChild(dayLabel);
        });
        monthContainer.appendChild(daysHeader);

        const daysGrid = document.createElement("div");
        daysGrid.className = "month-days-grid";

        const primerDiaSemana = new Date(currentYear, mesIndex, 1).getDay(); // 0 es Domingo
        const startOffset = (primerDiaSemana === 0) ? 6 : primerDiaSemana - 1;
        const totalDias = new Date(currentYear, mesIndex + 1, 0).getDate();

        for (let i = 0; i < startOffset; i++) {
            const emptyCell = document.createElement("div");
            emptyCell.className = "day-cell empty";
            daysGrid.appendChild(emptyCell);
        }

        for (let d = 1; d <= totalDias; d++) {
            const dayCell = document.createElement("div");
            dayCell.className = "day-cell";
            dayCell.dataset.day = d;
            dayCell.dataset.month = mesIndex + 1;
            
            const spanNum = document.createElement("span");
            spanNum.textContent = d;
            dayCell.appendChild(spanNum);

            const dateStr = `${d.toString().padStart(2, '0')}/${(mesIndex + 1).toString().padStart(2, '0')}/${currentYear}`;
            
            const dayOfWeek = new Date(currentYear, mesIndex, d).getDay();
            const esFinSemana = (dayOfWeek === 0 || dayOfWeek === 6);

            if (esFinSemana) {
                dayCell.classList.add("weekend");
            }

            if (festivos.includes(dateStr)) {
                dayCell.classList.add("festivo");
            }

            // Buscar vacaciones solapadas
            const trabajadoresEnFecha = [];
            Object.keys(trabajadores).forEach(tName => {
                if (trabajadores[tName].vacaciones.includes(dateStr)) {
                    trabajadoresEnFecha.push(tName);
                }
            });

            if (trabajadoresEnFecha.length > 0) {
                if (editMode === "vacaciones" && trabajadoresEnFecha.includes(activeWorker)) {
                    dayCell.classList.add("vacacion");
                } else {
                    dayCell.classList.add("vacacion-solapada");
                }

                // Renderizar los chips de iniciales de los trabajadores
                const dotsContainer = document.createElement("div");
                dotsContainer.className = "day-worker-dots";
                
                const maxChips = 2;
                const mostrables = trabajadoresEnFecha.slice(0, maxChips);
                mostrables.forEach(tName => {
                    const dot = document.createElement("span");
                    dot.className = "worker-dot";
                    
                    const partes = tName.split(" ");
                    let ini = (partes.length >= 2) ? (partes[0][0] + partes[1][0]) : partes[0].substring(0, 2);
                    dot.textContent = ini.toUpperCase();
                    dotsContainer.appendChild(dot);
                });

                if (trabajadoresEnFecha.length > maxChips) {
                    const moreDot = document.createElement("span");
                    moreDot.className = "worker-dot more-chip";
                    moreDot.textContent = `+${trabajadoresEnFecha.length - maxChips}`;
                    dotsContainer.appendChild(moreDot);
                }

                dayCell.appendChild(dotsContainer);

                // Eventos del Tooltip interactivo flotante moderno
                dayCell.addEventListener("mouseenter", (e) => {
                    const tooltip = document.getElementById("tooltip-flotante");
                    if (tooltip) {
                        const nombresHtml = trabajadoresEnFecha.map(n => `• ${n}`).join("<br>");
                        tooltip.innerHTML = `<strong>Vacaciones (${dateStr}):</strong><br>${nombresHtml}`;
                        tooltip.style.display = "block";
                    }
                });

                dayCell.addEventListener("mousemove", (e) => {
                    const tooltip = document.getElementById("tooltip-flotante");
                    if (tooltip) {
                        tooltip.style.left = (e.pageX + 12) + "px";
                        tooltip.style.top = (e.pageY + 12) + "px";
                    }
                });

                dayCell.addEventListener("mouseleave", () => {
                    const tooltip = document.getElementById("tooltip-flotante");
                    if (tooltip) {
                        tooltip.style.display = "none";
                    }
                });
            }

            // Eventos para click y arrastre
            dayCell.addEventListener("mousedown", (e) => onCellMouseDown(e, dateStr, dayCell));
            dayCell.addEventListener("mouseenter", (e) => onCellMouseEnter(e, dateStr, dayCell));

            daysGrid.appendChild(dayCell);
        }

        monthContainer.appendChild(daysGrid);
        monthsGrid.appendChild(monthContainer);
    });
}

// ==========================================
// ACCIONES DE SELECCIÓN Y DRAG
// ==========================================
function onCellMouseDown(e, dateStr, cellElement) {
    if (editMode === "vacaciones" && (!activeWorker || !trabajadores[activeWorker])) {
        alert("Por favor, selecciona o añade un trabajador primero.");
        return;
    }

    isDragging = true;
    dragSelectionType = editMode;

    let estaSeleccionado = false;
    if (editMode === "festivos") {
        estaSeleccionado = festivos.includes(dateStr);
    } else {
        estaSeleccionado = trabajadores[activeWorker].vacaciones.includes(dateStr);
    }

    dragAction = estaSeleccionado ? "deselect" : "select";
    procesarDia(dateStr, dragAction);
}

function onCellMouseEnter(e, dateStr, cellElement) {
    if (!isDragging || dragSelectionType !== editMode) return;
    procesarDia(dateStr, dragAction);
}

function procesarDia(dateStr, accion) {
    const parts = dateStr.split("/");
    const date = new Date(parseInt(parts[2]), parseInt(parts[1]) - 1, parseInt(parts[0]));
    const esLaborable = (date.getDay() !== 0 && date.getDay() !== 6 && !festivos.includes(dateStr));

    if (editMode === "festivos") {
        if (accion === "deselect") {
            const idx = festivos.indexOf(dateStr);
            if (idx > -1) festivos.splice(idx, 1);
        } else {
            // Eliminar de las vacaciones de cualquier trabajador si se vuelve festivo
            Object.keys(trabajadores).forEach(tName => {
                const idxV = trabajadores[tName].vacaciones.indexOf(dateStr);
                if (idxV > -1) trabajadores[tName].vacaciones.splice(idxV, 1);
            });
            if (!festivos.includes(dateStr)) {
                festivos.push(dateStr);
            }
        }
    } else {
        if (!activeWorker || !trabajadores[activeWorker]) return;

        if (accion === "deselect") {
            const idx = trabajadores[activeWorker].vacaciones.indexOf(dateStr);
            if (idx > -1) trabajadores[activeWorker].vacaciones.splice(idx, 1);
        } else {
            const info = trabajadores[activeWorker];
            const cupoTotal = info.dias_base + info.dias_extras;
            
            if (esLaborable && !info.vacaciones.includes(dateStr)) {
                const consumidos = contarDiasConsumidos(activeWorker);
                if (consumidos >= cupoTotal && !isDragging) {
                    const confirmar = confirm(`El trabajador "${activeWorker}" ya ha consumido su cupo disponible (${cupoTotal} días).\n¿Deseas añadir este día extra de todos modos?`);
                    if (!confirmar) return;
                }
            }

            const idxF = festivos.indexOf(dateStr);
            if (idxF > -1) festivos.splice(idxF, 1);
            
            if (!info.vacaciones.includes(dateStr)) {
                info.vacaciones.push(dateStr);
            }
        }
    }

    actualizarVistas();
    actualizarPanelCupo();
}

// ==========================================
// RENDERIZADO DE TABLA GANTT
// ==========================================
function renderGantt() {
    const container = document.getElementById("gantt-table-container");
    container.innerHTML = "";

    let todasFechas = [];
    Object.keys(trabajadores).forEach(tName => {
        todasFechas = todasFechas.concat(trabajadores[tName].vacaciones);
    });

    let minDate, maxDate;
    if (todasFechas.length > 0) {
        const parsedDates = todasFechas.map(f => {
            const p = f.split("/");
            return new Date(parseInt(p[2]), parseInt(p[1]) - 1, parseInt(p[0]));
        });
        minDate = new Date(Math.min(...parsedDates));
        maxDate = new Date(Math.max(...parsedDates));
    } else {
        minDate = new Date(currentYear, 5, 1);
        maxDate = new Date(currentYear, 8, 30);
    }

    const startYear = minDate.getFullYear();
    const startMonth = minDate.getMonth();
    const endYear = maxDate.getFullYear();
    const endMonth = maxDate.getMonth();

    const mesesSecuencia = [];
    let curY = startYear;
    let curM = startMonth;
    while (curY < endYear || (curY === endYear && curM <= endMonth)) {
        mesesSecuencia.push({ year: curY, month: curM });
        curM++;
        if (curM === 12) {
            curM = 0;
            curY++;
        }
    }

    const fechasEjeX = [];
    mesesSecuencia.forEach(mObj => {
        const totalDias = new Date(mObj.year, mObj.month + 1, 0).getDate();
        for (let d = 1; d <= totalDias; d++) {
            fechasEjeX.push(new Date(mObj.year, mObj.month, d));
        }
    });

    if (fechasEjeX.length === 0) return;

    const table = document.createElement("table");
    table.className = "gantt-table";

    // Fila de Meses
    const trMeses = document.createElement("tr");
    const thTitle = document.createElement("th");
    thTitle.textContent = "MES";
    thTitle.className = "gantt-header-month";
    trMeses.appendChild(thTitle);

    mesesSecuencia.forEach(mObj => {
        const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
        const diasEnMes = fechasEjeX.filter(d => d.getFullYear() === mObj.year && d.getMonth() === mObj.month).length;
        const thMes = document.createElement("th");
        thMes.textContent = `${nombresMeses[mObj.month].toUpperCase()}`;
        thMes.colSpan = diasEnMes;
        thMes.className = "gantt-header-month";
        trMeses.appendChild(thMes);
    });
    table.appendChild(trMeses);

    // Fila de Días
    const trDias = document.createElement("tr");
    const thWorkerLabel = document.createElement("th");
    thWorkerLabel.textContent = "TRABAJADOR";
    thWorkerLabel.className = "gantt-header-day";
    trDias.appendChild(thWorkerLabel);

    fechasEjeX.forEach(date => {
        const thDia = document.createElement("th");
        thDia.textContent = date.getDate();
        thDia.className = "gantt-header-day";
        trDias.appendChild(thDia);
    });
    table.appendChild(trDias);

    // Filas por cada Trabajador
    Object.keys(trabajadores).sort().forEach(tName => {
        const trWorker = document.createElement("tr");
        
        const tdName = document.createElement("td");
        tdName.textContent = tName;
        tdName.className = "gantt-worker-name";
        trWorker.appendChild(tdName);

        const listVacaciones = trabajadores[tName].vacaciones;

        fechasEjeX.forEach(date => {
            const tdCell = document.createElement("td");
            tdCell.className = "gantt-cell";

            const dStr = `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')}/${date.getFullYear()}`;
            const esWeekend = (date.getDay() === 0 || date.getDay() === 6);
            const esFestivo = festivos.includes(dStr);
            const esVacacion = listVacaciones.includes(dStr);

            if (esVacacion) {
                tdCell.classList.add("vacacion-cell");
            } else if (esFestivo || esWeekend) {
                tdCell.classList.add("festivo-cell");
            }

            trWorker.appendChild(tdCell);
        });

        table.appendChild(trWorker);
    });

    container.appendChild(table);

    // Leyenda y Cómputos
    const legend = document.createElement("div");
    legend.className = "gantt-legend";
    legend.innerHTML = `
        <div class="legend-item">
            <div class="legend-color" style="background-color: #E2E8F0;"></div>
            <span>Fin de semana / Festivos</span>
        </div>
        <div class="legend-item">
            <div class="legend-color" style="background-color: var(--color-vacacion-base);"></div>
            <span>Vacaciones del personal</span>
        </div>
    `;
    container.appendChild(legend);

    const computoContainer = document.createElement("div");
    computoContainer.style.marginTop = "15px";
    computoContainer.style.fontSize = "12px";
    
    const h3 = document.createElement("h3");
    h3.textContent = "Cómputo total de vacaciones (Días laborables netos en el año):";
    h3.style.marginBottom = "6px";
    h3.style.fontSize = "13px";
    h3.style.fontWeight = "700";
    computoContainer.appendChild(h3);

    const ul = document.createElement("ul");
    ul.style.listStyleType = "none";
    ul.style.paddingLeft = "10px";

    Object.keys(trabajadores).sort().forEach(tName => {
        const netos = contarDiasConsumidos(tName);
        const li = document.createElement("li");
        li.textContent = `• ${tName}: ${netos} días laborables netos de vacaciones disfrutados.`;
        li.style.marginBottom = "3px";
        ul.appendChild(li);
    });
    computoContainer.appendChild(ul);
    container.appendChild(computoContainer);
}

// ==========================================
// ACTUALIZAR CONSOLA DE RESUMEN
// ==========================================
function actualizarResumen() {
    const consoleDiv = document.getElementById("txt-resumen");
    let content = "=== FESTIVOS ===\n";

    const festivosOrdenados = [...festivos].sort((a,b) => {
        const ap = a.split("/"); const bp = b.split("/");
        return new Date(ap[2], ap[1]-1, ap[0]) - new Date(bp[2], bp[1]-1, bp[0]);
    });

    festivosOrdenados.forEach(f => {
        content += `- ${f}\n`;
    });

    Object.keys(trabajadores).sort().forEach(tName => {
        const info = trabajadores[tName];
        const disp = info.dias_base + info.dias_extras;
        const cons = contarDiasConsumidos(tName);
        
        content += `\n=== ${tName.toUpperCase()} (Consumidos: ${cons}/${disp}) ===\n`;

        const vacsOrdenadas = [...info.vacaciones].sort((a,b) => {
            const ap = a.split("/"); const bp = b.split("/");
            return new Date(ap[2], ap[1]-1, ap[0]) - new Date(bp[2], bp[1]-1, bp[0]);
        });

        vacsOrdenadas.forEach(v => {
            const parts = v.split("/");
            const date = new Date(parts[2], parts[1]-1, parts[0]);
            let extra = "";
            if (date.getDay() === 0 || date.getDay() === 6) {
                extra = " (Fin de semana - No computa)";
            } else if (festivos.includes(v)) {
                extra = " (Festivo oficial - No computa)";
            }
            content += `- ${v}${extra}\n`;
        });
    });

    if (consoleDiv) {
        consoleDiv.textContent = content;
    }

    // Actualizar también el panel de rangos en texto
    const textPanel = document.getElementById("txt-vacaciones-texto");
    if (textPanel) {
        textPanel.innerHTML = "";
        const workerNames = Object.keys(trabajadores).sort();
        
        if (workerNames.length === 0) {
            textPanel.innerHTML = "<div class='text-muted' style='font-style: italic; color: var(--text-muted);'>No hay personal registrado en el sistema.</div>";
        } else {
            workerNames.forEach(tName => {
                const info = trabajadores[tName];
                const rangosTexto = agruparVacacionesEnTexto(info.vacaciones, festivos, currentYear);
                
                const item = document.createElement("div");
                item.className = "text-vacations-item";
                
                const title = document.createElement("div");
                title.className = "text-vacations-worker";
                title.textContent = tName;
                
                const desc = document.createElement("div");
                desc.className = "text-vacations-ranges";
                desc.textContent = rangosTexto;
                
                item.appendChild(title);
                item.appendChild(desc);
                textPanel.appendChild(item);
            });
        }
    }
}

// Exportar ficheros
function exportData(type, format) {
    let filename = `export_${type}_${currentYear}.${format}`;
    let fileContent = "";

    if (format === "json") {
        let exportObj = {};
        if (type === "trabajadores") {
            Object.keys(trabajadores).forEach(t => {
                exportObj[t] = {
                    dias_base: trabajadores[t].dias_base,
                    dias_extras: trabajadores[t].dias_extras
                };
            });
        } else if (type === "festivos") {
            exportObj = [...festivos].sort();
        } else if (type === "vacaciones") {
            Object.keys(trabajadores).forEach(t => {
                exportObj[t] = [...trabajadores[t].vacaciones].sort();
            });
        }
        fileContent = JSON.stringify(exportObj, null, 4);
    } else {
        if (type === "trabajadores") {
            Object.keys(trabajadores).forEach(t => {
                fileContent += `"${t}",${trabajadores[t].dias_base},${trabajadores[t].dias_extras}\n`;
            });
        } else if (type === "festivos") {
            [...festivos].sort().forEach(f => {
                fileContent += `"${f}"\n`;
            });
        } else if (type === "vacaciones") {
            Object.keys(trabajadores).forEach(t => {
                const vacSorted = [...trabajadores[t].vacaciones].sort();
                if (vacSorted.length > 0) {
                    fileContent += `"${t}",${vacSorted.map(f => `"${f}"`).join(",")}\n`;
                } else {
                    fileContent += `"${t}"\n`;
                }
            });
        }
    }

    guardarArchivoDescarga(filename, fileContent, false);
}

// ==========================================
// IMPORTADOR INTELIGENTE DE DATOS
// ==========================================
function importarDesdeTexto(text, es_json) {
    if (es_json) {
        let data;
        try {
            data = JSON.parse(text);
        } catch (e) {
            throw new Error("Formato JSON inválido: " + e.message);
        }

        // 1. Detectar si es el JSON Consolidado
        if (data && typeof data === 'object' && !Array.isArray(data) && 
            ('trabajadores' in data || 'festivos' in data || 'titulo_pagina' in data)) {
            
            if ('titulo_pagina' in data) {
                const titleInput = document.getElementById("page-title-input");
                if (titleInput) titleInput.value = data.titulo_pagina;
            }
            if ('year' in data) {
                currentYear = parseInt(data.year) || 2026;
                document.getElementById("label-year").textContent = currentYear;
            }
            if ('festivos' in data) {
                festivos = Array.isArray(data.festivos) ? data.festivos : [];
            }
            if ('trabajadores' in data) {
                trabajadores = {};
                for (let nombre in data.trabajadores) {
                    if (!esNombreValidoTrabajador(nombre)) continue;
                    let info = data.trabajadores[nombre];
                    trabajadores[nombre] = {
                        vacaciones: Array.isArray(info.vacaciones) ? info.vacaciones : [],
                        dias_base: typeof info.dias_base !== 'undefined' ? parseInt(info.dias_base) : 22,
                        dias_extras: typeof info.dias_extras !== 'undefined' ? parseInt(info.dias_extras) : 0
                    };
                }
            }
            return { tipo: "Consolidado Completo", msg: "Se ha importado el estado completo del planificador." };
        }

        // 2. Detectar si es Festivos (Lista de fechas simples)
        if (Array.isArray(data)) {
            let count = 0;
            data.forEach(item => {
                if (typeof item === 'string') {
                    if (/^\d{2}\/\d{2}\/\d{4}$/.test(item)) {
                        if (!festivos.includes(item)) {
                            festivos.push(item);
                            count++;
                            for (let t in trabajadores) {
                                let idx = trabajadores[t].vacaciones.indexOf(item);
                                if (idx > -1) trabajadores[t].vacaciones.splice(idx, 1);
                            }
                        }
                    }
                }
            });
            return { tipo: "Festivos Oficiales", msg: `Se han importado ${count} festivos oficiales nuevos.` };
        }

        // 3. Detectar si es Configuración de Trabajadores o Vacaciones Asignadas
        if (data && typeof data === 'object') {
            let es_config_trabajadores = false;
            for (let k in data) {
                let v = data[k];
                if (v && typeof v === 'object' && !Array.isArray(v) && ('dias_base' in v || 'dias_extras' in v)) {
                    es_config_trabajadores = true;
                    break;
                }
            }

            if (es_config_trabajadores) {
                let count = 0;
                for (let nombre in data) {
                    if (!esNombreValidoTrabajador(nombre)) continue;
                    let info = data[nombre];
                    let d_base = (info && typeof info.dias_base !== 'undefined') ? parseInt(info.dias_base) : 22;
                    let d_extras = (info && typeof info.dias_extras !== 'undefined') ? parseInt(info.dias_extras) : 0;
                    if (trabajadores[nombre]) {
                        trabajadores[nombre].dias_base = d_base;
                        trabajadores[nombre].dias_extras = d_extras;
                    } else {
                        trabajadores[nombre] = {
                            vacaciones: [],
                            dias_base: d_base,
                            dias_extras: d_extras
                        };
                    }
                    count++;
                }
                return { tipo: "Configuración de Personal", msg: `Se han importado/actualizado ${count} perfiles de trabajadores.` };
            } else {
                // Vacaciones asignadas (objeto de listas de fechas)
                let count_w = 0;
                for (let nombre in data) {
                    if (!esNombreValidoTrabajador(nombre)) continue;
                    let fechas_list = data[nombre];
                    let fechas_str = Array.isArray(fechas_list) ? fechas_list : (fechas_list && Array.isArray(fechas_list.vacaciones) ? fechas_list.vacaciones : null);
                    if (!fechas_str) continue;

                    let fechas_validas = [];
                    fechas_str.forEach(f_str => {
                        if (/^\d{2}\/\d{2}\/\d{4}$/.test(f_str)) {
                            if (!festivos.includes(f_str)) {
                                fechas_validas.push(f_str);
                            }
                        }
                    });

                    if (!trabajadores[nombre]) {
                        trabajadores[nombre] = { vacaciones: [], dias_base: 22, dias_extras: 0 };
                    }
                    trabajadores[nombre].vacaciones = fechas_validas;
                    count_w++;
                }
                return { tipo: "Vacaciones Asignadas", msg: `Se han importado vacaciones para ${count_w} trabajadores.` };
            }
        }
        throw new Error("Estructura JSON no reconocida");
    } else {
        // CSV Parser Inteligente
        let lines = text.split(/\r?\n/);
        let filas = [];
        lines.forEach(line => {
            if (!line.trim()) return;
            let row = [];
            let inQuotes = false;
            let currentToken = "";
            for (let i = 0; i < line.length; i++) {
                let char = line[i];
                if (char === '"') {
                    inQuotes = !inQuotes;
                } else if (char === ',' && !inQuotes) {
                    row.push(currentToken.trim());
                    currentToken = "";
                } else {
                    currentToken += char;
                }
            }
            row.push(currentToken.trim());
            filas.push(row);
        });

        if (filas.length === 0) throw new Error("Archivo CSV vacío");

        let primera_fila = filas[0];

        // 1. Detectar si son Festivos (un elemento por fila con formato de fecha)
        let es_festivo = true;
        for (let i = 0; i < filas.length; i++) {
            let row = filas[i];
            if (row.length !== 1 || !/^\d{2}\/\d{2}\/\d{4}$/.test(row[0])) {
                es_festivo = false;
                break;
            }
        }

        if (es_festivo) {
            let count = 0;
            filas.forEach(row => {
                let dateStr = row[0];
                if (!festivos.includes(dateStr)) {
                    festivos.push(dateStr);
                    count++;
                    for (let t in trabajadores) {
                        let idx = trabajadores[t].vacaciones.indexOf(dateStr);
                        if (idx > -1) trabajadores[t].vacaciones.splice(idx, 1);
                    }
                }
            });
            return { tipo: "Festivos Oficiales (CSV)", msg: `Se han importado ${count} festivos oficiales.` };
        }

        // 2. Distinguir entre Configuración de Trabajadores (Nombre, dias_base, dias_extras)
        // y Vacaciones (Nombre, fecha1, fecha2, ...)
        let es_config = false;
        if (primera_fila.length >= 2) {
            let segundoVal = primera_fila[1];
            if (!isNaN(segundoVal) && !isNaN(parseInt(segundoVal))) {
                es_config = true;
            }
        }

        if (es_config) {
            let count = 0;
            filas.forEach(row => {
                let nombre = row[0];
                if (!esNombreValidoTrabajador(nombre)) return;
                let d_base = (row.length > 1 && !isNaN(row[1]) && row[1] !== "") ? parseInt(row[1]) : 22;
                let d_extras = (row.length > 2 && !isNaN(row[2]) && row[2] !== "") ? parseInt(row[2]) : 0;
                if (trabajadores[nombre]) {
                    trabajadores[nombre].dias_base = d_base;
                    trabajadores[nombre].dias_extras = d_extras;
                } else {
                    trabajadores[nombre] = {
                        vacaciones: [],
                        dias_base: d_base,
                        dias_extras: d_extras
                    };
                }
                count++;
            });
            return { tipo: "Configuración de Personal (CSV)", msg: `Se han importado ${count} perfiles de trabajadores.` };
        } else {
            // Vacaciones asignadas en formato CSV
            let count = 0;
            filas.forEach(row => {
                let nombre = row[0];
                if (!esNombreValidoTrabajador(nombre)) return;
                let fechas_validas = [];
                for (let i = 1; i < row.length; i++) {
                    let dateStr = row[i];
                    if (/^\d{2}\/\d{2}\/\d{4}$/.test(dateStr)) {
                        if (!festivos.includes(dateStr)) {
                            fechas_validas.push(dateStr);
                        }
                    }
                }
                if (!trabajadores[nombre]) {
                    trabajadores[nombre] = { vacaciones: [], dias_base: 22, dias_extras: 0 };
                }
                trabajadores[nombre].vacaciones = fechas_validas;
                count++;
            });
            return { tipo: "Vacaciones Asignadas (CSV)", msg: `Se han cargado vacaciones para ${count} trabajadores.` };
        }
    }
}

// ==========================================
// EXPORTACIÓN DE GANTT A HOJA DE CÁLCULO
// ==========================================
function obtenerSecuenciaGantt() {
    let todasFechas = [];
    Object.keys(trabajadores).forEach(tName => {
        todasFechas = todasFechas.concat(trabajadores[tName].vacaciones);
    });

    let minDate, maxDate;
    if (todasFechas.length > 0) {
        const parsedDates = todasFechas.map(f => {
            const p = f.split("/");
            return new Date(parseInt(p[2]), parseInt(p[1]) - 1, parseInt(p[0]));
        });
        minDate = new Date(Math.min(...parsedDates));
        maxDate = new Date(Math.max(...parsedDates));
    } else {
        minDate = new Date(currentYear, 5, 1);
        maxDate = new Date(currentYear, 8, 30);
    }

    const startYear = minDate.getFullYear();
    const startMonth = minDate.getMonth();
    const endYear = maxDate.getFullYear();
    const endMonth = maxDate.getMonth();

    const mesesSecuencia = [];
    let curY = startYear;
    let curM = startMonth;
    while (curY < endYear || (curY === endYear && curM <= endMonth)) {
        mesesSecuencia.push({ year: curY, month: curM });
        curM++;
        if (curM === 12) {
            curM = 0;
            curY++;
        }
    }

    const fechasEjeX = [];
    mesesSecuencia.forEach(mObj => {
        const totalDias = new Date(mObj.year, mObj.month + 1, 0).getDate();
        for (let d = 1; d <= totalDias; d++) {
            fechasEjeX.push(new Date(mObj.year, mObj.month, d));
        }
    });

    return { mesesSecuencia, fechasEjeX };
}

function exportarGanttACSV() {
    const { mesesSecuencia, fechasEjeX } = obtenerSecuenciaGantt();
    if (fechasEjeX.length === 0) {
        alert("No hay fechas que exportar.");
        return;
    }

    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    
    let csvContent = "";

    // 1. Fila de Meses
    let filaMeses = ["MES"];
    mesesSecuencia.forEach(mObj => {
        const diasEnMes = fechasEjeX.filter(d => d.getFullYear() === mObj.year && d.getMonth() === mObj.month).length;
        const etiquetaMes = `${nombresMeses[mObj.month].toUpperCase()} ${mObj.year}`;
        filaMeses.push(etiquetaMes);
        for (let i = 1; i < diasEnMes; i++) {
            filaMeses.push(""); // Celdas vacías para simular el merge
        }
    });
    csvContent += filaMeses.map(x => `"${x}"`).join(",") + "\n";

    // 2. Fila de Días
    let filaDias = ["TRABAJADOR"];
    fechasEjeX.forEach(date => {
        filaDias.push(date.getDate());
    });
    csvContent += filaDias.map(x => `"${x}"`).join(",") + "\n";

    // 3. Filas de Trabajadores
    Object.keys(trabajadores).sort().forEach(tName => {
        let filaWorker = [tName];
        const listVacaciones = trabajadores[tName].vacaciones;

        fechasEjeX.forEach(date => {
            const dStr = `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')}/${date.getFullYear()}`;
            const esWeekend = (date.getDay() === 0 || date.getDay() === 6);
            const esFestivo = festivos.includes(dStr);
            const esVacacion = listVacaciones.includes(dStr);

            if (esVacacion) {
                filaWorker.push("V"); // Vacaciones
            } else if (esFestivo || esWeekend) {
                filaWorker.push("F"); // Festivo / Fin de Semana
            } else {
                filaWorker.push("");
            }
        });
        csvContent += filaWorker.map(x => `"${x}"`).join(",") + "\n";
    });

    guardarArchivoDescarga(`calendario_vacaciones_gantt_${currentYear}.csv`, csvContent, false);
}

function exportarGanttAExcel() {
    const { mesesSecuencia, fechasEjeX } = obtenerSecuenciaGantt();
    if (fechasEjeX.length === 0) {
        alert("No hay fechas que exportar.");
        return;
    }

    const filename = `calendario_vacaciones_gantt_${currentYear}.xls`;

    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    let html = `
    <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
    <head>
        <meta charset="utf-8">
        <style>
            table { border-collapse: collapse; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
            th, td { border: 1px solid #CBD5E1; padding: 6px; text-align: center; font-size: 11px; }
            .header-title { background-color: #6366F1; color: #FFFFFF; font-weight: bold; font-size: 13px; }
            .header-month { background-color: #475569; color: #FFFFFF; font-weight: bold; }
            .header-day { background-color: #94A3B8; color: #FFFFFF; font-weight: bold; }
            .worker-name { font-weight: bold; text-align: left; background-color: #F8FAFC; }
            .vacation-cell { background-color: #AED6F1; color: #1B4F72; }
            .festivo-cell { background-color: #E2E8F0; color: #64748B; }
        </style>
    </head>
    <body>
        <table>
            <!-- Fila de Meses -->
            <tr>
                <th class="header-month">MES</th>
    `;

    mesesSecuencia.forEach(mObj => {
        const diasEnMes = fechasEjeX.filter(d => d.getFullYear() === mObj.year && d.getMonth() === mObj.month).length;
        const etiquetaMes = `${nombresMeses[mObj.month].toUpperCase()} ${mObj.year}`;
        html += `<th class="header-month" colspan="${diasEnMes}">${etiquetaMes}</th>`;
    });

    html += `
            </tr>
            <!-- Fila de Días -->
            <tr>
                <th class="header-day">TRABAJADOR</th>
    `;

    fechasEjeX.forEach(date => {
        html += `<th class="header-day">${date.getDate()}</th>`;
    });

    html += `
            </tr>
            <!-- Filas por cada Trabajador -->
    `;

    Object.keys(trabajadores).sort().forEach(tName => {
        html += `<tr><td class="worker-name">${tName}</td>`;
        const listVacaciones = trabajadores[tName].vacaciones;

        fechasEjeX.forEach(date => {
            const dStr = `${date.getDate().toString().padStart(2, '0')}/${(date.getMonth() + 1).toString().padStart(2, '0')}/${date.getFullYear()}`;
            const esWeekend = (date.getDay() === 0 || date.getDay() === 6);
            const esFestivo = festivos.includes(dStr);
            const esVacacion = listVacaciones.includes(dStr);

            if (esVacacion) {
                html += `<td class="vacation-cell">V</td>`;
            } else if (esFestivo || esWeekend) {
                html += `<td class="festivo-cell">F</td>`;
            } else {
                html += `<td></td>`;
            }
        });
        html += `</tr>`;
    });

    html += `
        </table>
    </body>
    </html>
    `;

    guardarArchivoDescarga(filename, html, false);
}

// ==========================================
// GENERACIÓN DE REPORTES PDF VECTORIALES
// ==========================================
function obtenerIniciales(nombre) {
    const partes = nombre.trim().split(/\s+/);
    if (partes.length >= 2) {
        return (partes[0][0] + partes[1][0]).toUpperCase();
    } else if (partes.length === 1 && partes[0]) {
        return partes[0].substring(0, 2).toUpperCase();
    }
    return '';
}

function getMonthWeeks(year, month) { // month es 1-based (1 = Enero)
    const weeks = [];
    const firstDay = new Date(year, month - 1, 1);
    const lastDay = new Date(year, month, 0);
    
    let dayOfWeek = firstDay.getDay(); // 0 es Domingo
    let startOffset = (dayOfWeek === 0) ? 6 : dayOfWeek - 1; // Lunes = 0
    
    let currentWeek = Array(7).fill(0);
    let dayCounter = 1;
    
    for (let i = startOffset; i < 7; i++) {
        currentWeek[i] = dayCounter++;
    }
    weeks.push(currentWeek);
    
    while (dayCounter <= lastDay.getDate()) {
        currentWeek = Array(7).fill(0);
        for (let i = 0; i < 7 && dayCounter <= lastDay.getDate(); i++) {
            currentWeek[i] = dayCounter++;
        }
        weeks.push(currentWeek);
    }
    return weeks;
}

function generarPdfMensual() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
    const yearStr = currentYear.toString();
    const titleInput = document.getElementById("page-title-input");
    const docTitle = titleInput ? titleInput.value.trim() : "Planificación de Vacaciones";

    function drawHeaderFooter(pageDoc, pNum) {
        pageDoc.setFont('helvetica', 'bold');
        pageDoc.setFontSize(14);
        pageDoc.setTextColor(44, 62, 80);
        pageDoc.text(`${docTitle} - ${yearStr}`, 12, 15);
        
        const now = new Date();
        const dateStr = `${now.getDate().toString().padStart(2, '0')}/${(now.getMonth() + 1).toString().padStart(2, '0')}/${now.getFullYear()}`;
        pageDoc.setFont('helvetica', 'italic');
        pageDoc.setFontSize(9);
        pageDoc.setTextColor(100, 110, 120);
        pageDoc.text(`Generado: ${dateStr}`, 198, 15, { align: 'right' });
        
        pageDoc.setDrawColor(200, 200, 200);
        pageDoc.setLineWidth(0.3);
        pageDoc.line(12, 18, 198, 18);
        
        pageDoc.setFont('helvetica', 'normal');
        pageDoc.setFontSize(8);
        pageDoc.setTextColor(127, 140, 141);
        pageDoc.setDrawColor(220, 220, 220);
        pageDoc.line(12, 285, 198, 285);
        pageDoc.text("Gestor de Vacaciones Pro", 12, 290);
        pageDoc.text(`Página ${pNum}`, 198, 290, { align: 'right' });
    }

    drawHeaderFooter(doc, 1);
    
    const meses = [6, 7, 8, 9]; // Junio a Septiembre
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    const daysHeader = ["L", "M", "X", "J", "V", "S", "D"];
    
    const margin_left = 12;
    const col_width = 86;
    const gap_x = 14;
    const gap_y = 12;
    const row_height_blocks = 50;
    const start_y = 26;

    meses.forEach((month, index) => {
        const col = index % 2;
        const row = Math.floor(index / 2);
        
        const x_start = margin_left + col * (col_width + gap_x);
        const y_start = start_y + row * (row_height_blocks + gap_y);
        
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(11);
        doc.setTextColor(52, 73, 94);
        doc.text(nombresMeses[month - 1], x_start + col_width / 2, y_start + 5, { align: 'center' });
        
        const cell_w = col_width / 7;
        const cell_h = 6;
        
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8.5);
        doc.setTextColor(100, 110, 120);
        doc.setFillColor(242, 244, 244);
        
        let cur_x = x_start;
        let cur_y = y_start + 8;
        
        daysHeader.forEach(day => {
            doc.setFillColor(242, 244, 244);
            doc.setTextColor(100, 110, 120);
            doc.rect(cur_x, cur_y, cell_w, cell_h, 'FD');
            doc.text(day, cur_x + cell_w / 2, cur_y + 4.2, { align: 'center' });
            cur_x += cell_w;
        });
        
        cur_y += cell_h;
        
        const weeks = getMonthWeeks(currentYear, month);
        weeks.forEach(week => {
            cur_x = x_start;
            week.forEach((day, dIdx) => {
                if (day === 0) {
                    doc.setFillColor(255, 255, 255);
                    doc.setDrawColor(200, 200, 200);
                    doc.rect(cur_x, cur_y, cell_w, cell_h, 'S');
                } else {
                    const dateStr = `${day.toString().padStart(2, '0')}/${month.toString().padStart(2, '0')}/${currentYear}`;
                    const esFinDeSemana = (dIdx >= 5);
                    const esFestivoOficial = festivos.includes(dateStr);
                    
                    let fillColor = [255, 255, 255];
                    let textColor = [44, 62, 80];
                    let isFilled = false;
                    let fontStyle = 'normal';
                    let fontSize = 8.5;
                    let cellText = day.toString();
                    
                    if (esFinDeSemana || esFestivoOficial) {
                        fillColor = [244, 246, 247];
                        textColor = [231, 76, 60];
                        isFilled = true;
                    }
                    
                    const trabsVac = [];
                    for (let tName in trabajadores) {
                        if (trabajadores[tName].vacaciones.includes(dateStr)) {
                            trabsVac.push(tName);
                        }
                    }
                    
                    if (trabsVac.length > 0) {
                        fillColor = [174, 214, 241];
                        textColor = [27, 79, 114];
                        isFilled = true;
                        
                        if (trabsVac.length === 1) {
                            const ini = obtenerIniciales(trabsVac[0]);
                            cellText = `${day}(${ini})`;
                            fontStyle = 'bold';
                            fontSize = 7;
                        } else if (trabsVac.length === 2) {
                            const ini1 = obtenerIniciales(trabsVac[0]);
                            const ini2 = obtenerIniciales(trabsVac[1]);
                            cellText = `${day}(${ini1},${ini2})`;
                            fontStyle = 'bold';
                            fontSize = 6;
                        } else {
                            const ini1 = obtenerIniciales(trabsVac[0]);
                            const rest = trabsVac.length - 1;
                            cellText = `${day}(${ini1}+${rest})`;
                            fontStyle = 'bold';
                            fontSize = 6;
                        }
                    }
                    
                    doc.setFillColor(fillColor[0], fillColor[1], fillColor[2]);
                    doc.setTextColor(textColor[0], textColor[1], textColor[2]);
                    doc.setFont('helvetica', fontStyle);
                    doc.setFontSize(fontSize);
                    doc.setDrawColor(200, 200, 200);
                    
                    doc.rect(cur_x, cur_y, cell_w, cell_h, isFilled ? 'FD' : 'S');
                    const textY = cur_y + (cell_h / 2) + (fontSize * 0.35 / 2.83);
                    doc.text(cellText, cur_x + cell_w / 2, textY - 0.3, { align: 'center' });
                }
                cur_x += cell_w;
            });
            cur_y += cell_h;
        });
    });

    doc.addPage();
    drawHeaderFooter(doc, 2);
    
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(13);
    doc.setTextColor(44, 62, 80);
    doc.text("Resumen de Vacaciones y Leyenda", 12, 28);
    
    doc.setFillColor(174, 214, 241);
    doc.setDrawColor(200, 200, 200);
    doc.rect(12, 34, 18, 6, 'FD');
    doc.setTextColor(27, 79, 114);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.text("Día(XX)", 21, 38.2, { align: 'center' });
    
    doc.setTextColor(44, 62, 80);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9.5);
    doc.text("Días de vacaciones disfrutadas por el personal (Iniciales del empleado)", 34, 38.2);
    
    doc.setFillColor(244, 246, 247);
    doc.rect(12, 43, 18, 6, 'FD');
    doc.setTextColor(231, 76, 60);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.text("14", 21, 47.2, { align: 'center' });
    
    doc.setTextColor(44, 62, 80);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9.5);
    doc.text("Fines de semana o días festivos oficiales", 34, 47.2);
    
    doc.setDrawColor(220, 220, 220);
    doc.line(12, 54, 198, 54);
    
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text("Disfrute de Vacaciones (Días laborables netos consumidos en el año):", 12, 61);
    
    let text_y = 68;
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9.5);
    
    const wNames = Object.keys(trabajadores).sort();
    wNames.forEach(w => {
        const netos = contarDiasConsumidos(w);
        const limite = trabajadores[w].dias_base + trabajadores[w].dias_extras;
        const ini = obtenerIniciales(w);
        const excede = netos > limite ? " (Cupo superado!)" : "";
        const rangosTexto = agruparVacacionesEnTexto(trabajadores[w].vacaciones, festivos, currentYear);
        
        doc.setFont('helvetica', 'bold');
        doc.setTextColor(44, 62, 80);
        doc.text(`- [${ini}] ${w}: ${netos} de ${limite} días consumidos${excede}.`, 15, text_y);
        text_y += 4.5;
        
        doc.setFont('helvetica', 'italic');
        doc.setFontSize(8.5);
        doc.setTextColor(100, 110, 120);
        
        const lines = doc.splitTextToSize(`Vacaciones: ${rangosTexto}`, 175);
        lines.forEach(line => {
            if (text_y > 275) {
                doc.addPage();
                drawHeaderFooter(doc, doc.internal.getNumberOfPages());
                text_y = 28;
            }
            doc.text(line, 20, text_y);
            text_y += 4.5;
        });
        
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(9.5);
        doc.setTextColor(44, 62, 80);
        text_y += 2.0; // Espacio extra entre trabajadores
        
        if (text_y > 275) {
            doc.addPage();
            drawHeaderFooter(doc, doc.internal.getNumberOfPages());
            text_y = 28;
        }
    });

    const filename = `Calendario_Vacaciones_Mensual_${currentYear}.pdf`;
    if (isDesktop()) {
        const base64Data = doc.output('datauristring');
        guardarArchivoDescarga(filename, base64Data, true);
    } else {
        doc.save(filename);
    }
}

function generarPdfGantt() {
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
    const yearStr = currentYear.toString();
    const titleInput = document.getElementById("page-title-input");
    const docTitle = titleInput ? titleInput.value.trim() : "Planificación de Vacaciones";

    function drawHeaderFooter(pageDoc, pNum) {
        pageDoc.setFont('helvetica', 'bold');
        pageDoc.setFontSize(14);
        pageDoc.setTextColor(44, 62, 80);
        pageDoc.text(`${docTitle} - ${yearStr}`, 12, 15);
        
        const now = new Date();
        const dateStr = `${now.getDate().toString().padStart(2, '0')}/${(now.getMonth() + 1).toString().padStart(2, '0')}/${now.getFullYear()}`;
        pageDoc.setFont('helvetica', 'italic');
        pageDoc.setFontSize(9);
        pageDoc.setTextColor(100, 110, 120);
        pageDoc.text(`Generado: ${dateStr}`, 285, 15, { align: 'right' });
        
        pageDoc.setDrawColor(200, 200, 200);
        pageDoc.setLineWidth(0.3);
        pageDoc.line(12, 18, 285, 18);
        
        pageDoc.setFont('helvetica', 'normal');
        pageDoc.setFontSize(8);
        pageDoc.setTextColor(127, 140, 141);
        pageDoc.setDrawColor(220, 220, 220);
        pageDoc.line(12, 198, 285, 198);
        pageDoc.text("Gestor de Vacaciones Pro", 12, 203);
        pageDoc.text(`Página ${pNum}`, 285, 203, { align: 'right' });
    }

    let fechas = [];
    for (let t in trabajadores) {
        fechas = fechas.concat(trabajadores[t].vacaciones);
    }
    
    let startYear = currentYear, startMonth = 5;
    let endYear = currentYear, endMonth = 8;
    
    if (fechas.length > 0) {
        let minD = null, maxD = null;
        fechas.forEach(f => {
            const p = f.split("/");
            const d = new Date(parseInt(p[2]), parseInt(p[1]) - 1, parseInt(p[0]));
            if (!minD || d < minD) minD = d;
            if (!maxD || d > maxD) maxD = d;
        });
        startYear = minD.getFullYear();
        startMonth = minD.getMonth();
        endYear = maxD.getFullYear();
        endMonth = maxD.getMonth();
    }

    const mesesRango = [];
    let curr_y = startYear;
    let curr_m = startMonth;
    while (curr_y < endYear || (curr_y === endYear && curr_m <= endMonth)) {
        mesesRango.push({ year: curr_y, month: curr_m });
        curr_m++;
        if (curr_m === 12) {
            curr_m = 0;
            curr_y++;
        }
    }

    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    mesesRango.forEach((mObj, idx) => {
        if (idx > 0) {
            doc.addPage();
        }
        drawHeaderFooter(doc, idx + 1);

        const col_name_width = 38;
        const ancho_dias = 273 - col_name_width;
        const num_dias = new Date(mObj.year, mObj.month + 1, 0).getDate();
        const col_day_width = ancho_dias / num_dias;

        // Cabecera de mes
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(10);
        doc.setDrawColor(200, 200, 200);

        // Rectángulo MES
        doc.setFillColor(220, 225, 230);
        doc.rect(12, 24, col_name_width, 7, 'FD');
        doc.setTextColor(44, 62, 80);
        doc.text("MES", 12 + col_name_width / 2, 28.5, { align: 'center' });

        // Rectángulo Nombre de Mes
        doc.setFillColor(220, 225, 230);
        doc.rect(12 + col_name_width, 24, ancho_dias, 7, 'FD');
        doc.setTextColor(44, 62, 80);
        doc.text(`${nombresMeses[mObj.month].toUpperCase()} ${mObj.year}`, 12 + col_name_width + ancho_dias / 2, 28.5, { align: 'center' });

        // Cabecera de días (1 al N)
        let cur_y = 31;
        doc.setFont('helvetica', 'bold');
        doc.setFontSize(8);

        // Rectángulo TRABAJADOR
        doc.setFillColor(240, 242, 245);
        doc.rect(12, cur_y, col_name_width, 6, 'FD');
        doc.setTextColor(100, 110, 120);
        doc.text("TRABAJADOR", 14, cur_y + 4.2);

        for (let d = 1; d <= num_dias; d++) {
            const x = 12 + col_name_width + (d - 1) * col_day_width;
            doc.setFillColor(240, 242, 245);
            doc.rect(x, cur_y, col_day_width, 6, 'FD');
            doc.setTextColor(100, 110, 120);
            doc.text(d.toString(), x + col_day_width / 2, cur_y + 4.2, { align: 'center' });
        }

        cur_y += 6;

        // Filas de trabajadores
        const sortedWorkers = Object.keys(trabajadores).sort();
        sortedWorkers.forEach(w => {
            doc.setFont('helvetica', 'normal');
            doc.setFontSize(8.5);
            doc.setFillColor(252, 252, 252);
            doc.setTextColor(44, 62, 80);

            doc.rect(12, cur_y, col_name_width, 7, 'FD');
            doc.text(w, 14, cur_y + 4.7);

            const listVacaciones = trabajadores[w].vacaciones;

            for (let d = 1; d <= num_dias; d++) {
                const x = 12 + col_name_width + (d - 1) * col_day_width;
                const dStr = `${d.toString().padStart(2, '0')}/${(mObj.month + 1).toString().padStart(2, '0')}/${mObj.year}`;
                
                const testDate = new Date(mObj.year, mObj.month, d);
                const esWeekend = (testDate.getDay() === 0 || testDate.getDay() === 6);
                const esFestivo = festivos.includes(dStr);
                const esVacacion = listVacaciones.includes(dStr);

                let fillColor = [255, 255, 255];
                let isFilled = false;

                if (esVacacion) {
                    fillColor = [174, 214, 241];
                    isFilled = true;
                } else if (esFestivo || esWeekend) {
                    fillColor = [235, 237, 239];
                    isFilled = true;
                }

                doc.setFillColor(fillColor[0], fillColor[1], fillColor[2]);
                doc.rect(x, cur_y, col_day_width, 7, isFilled ? 'FD' : 'S');
            }
            cur_y += 7;
        });

        // Leyenda al pie
        cur_y += 5;
        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8);
        doc.setTextColor(100, 110, 120);

        doc.setFillColor(174, 214, 241);
        doc.rect(12, cur_y, 6, 4, 'FD');
        doc.text("Vacaciones del personal", 20, cur_y + 3);

        doc.setFillColor(235, 237, 239);
        doc.rect(62, cur_y, 6, 4, 'FD');
        doc.text("Fin de semana / Festivos", 70, cur_y + 3);
    });

    // Cómputo final en página aparte
    doc.addPage();
    drawHeaderFooter(doc, mesesRango.length + 1);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(12);
    doc.setTextColor(44, 62, 80);
    doc.text("Cómputo Anual de Vacaciones (Días laborables netos disfrutados):", 12, 28);

    let text_y = 36;
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9.5);

    const sortedWorkers = Object.keys(trabajadores).sort();
    sortedWorkers.forEach(w => {
        const netos = contarDiasConsumidos(w);
        const limite = trabajadores[w].dias_base + trabajadores[w].dias_extras;
        const excede = netos > limite ? " (Cupo superado!)" : "";
        const rangosTexto = agruparVacacionesEnTexto(trabajadores[w].vacaciones, festivos, currentYear);

        doc.setFont('helvetica', 'bold');
        doc.setTextColor(44, 62, 80);
        doc.text(`- ${w}: ${netos} días netos disfrutados de un cupo total de ${limite} días${excede}.`, 15, text_y);
        text_y += 4.5;

        doc.setFont('helvetica', 'italic');
        doc.setFontSize(8.5);
        doc.setTextColor(100, 110, 120);

        const lines = doc.splitTextToSize(`Vacaciones: ${rangosTexto}`, 265); // El landscape permite líneas más anchas (ancho de página A4 landscape es 297mm)
        lines.forEach(line => {
            if (text_y > 185) {
                doc.addPage();
                drawHeaderFooter(doc, doc.internal.getNumberOfPages());
                text_y = 28;
            }
            doc.text(line, 20, text_y);
            text_y += 4.5;
        });

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(9.5);
        doc.setTextColor(44, 62, 80);
        text_y += 2.0; // Espacio extra entre trabajadores

        if (text_y > 185) {
            doc.addPage();
            drawHeaderFooter(doc, doc.internal.getNumberOfPages());
            text_y = 28;
        }
    });

    const filename = `Calendario_Vacaciones_Tabla_${currentYear}.pdf`;
    if (isDesktop()) {
        const base64Data = doc.output('datauristring');
        guardarArchivoDescarga(filename, base64Data, true);
    } else {
        doc.save(filename);
    }
}
