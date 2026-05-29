/**
 * services.js - Servicios de la aplicación Calendario de Vacaciones
 * 
 * Contiene las clases de servicio responsables de:
 * - StorageService: Persistencia en localStorage y exportación de datos consolidados.
 * - HolidayService: Obtención de festivos oficiales desde la API pública OpenHolidays.
 * - ExportService: Exportación de datos a múltiples formatos (JSON, CSV, PDF Mensual, PDF Gantt).
 * 
 * IMPORTANTE: Este archivo se carga como script global (no ES module)
 * para evitar restricciones de CORS al abrir index.html directamente
 * desde el sistema de archivos (protocolo file://).
 * 
 * Dependencias globales requeridas (deben cargarse antes que este archivo):
 * - utils.js (DEFAULT_CONFIG, obtenerIniciales, getMonthWeeks, agruparVacacionesEnTexto, contarDiasConsumidos)
 * - jspdf (window.jspdf.jsPDF)
 */

// ============================================================================
// SERVICIO DE ALMACENAMIENTO LOCAL
// ============================================================================

/**
 * Gestiona la persistencia de datos en localStorage del navegador.
 * Usa una clave única para almacenar toda la configuración y datos de vacaciones.
 */
class StorageService {
    /** Clave del localStorage donde se persisten todos los datos */
    static STORAGE_KEY = 'CalendarioVacacionesData';

    /**
     * Carga los datos guardados en localStorage.
     * Si no existen datos previos o el JSON está corrupto,
     * devuelve una copia limpia de DEFAULT_CONFIG.
     * 
     * Se asegura de que todos los campos de DEFAULT_CONFIG existan
     * fusionando los datos guardados con la configuración por defecto.
     * 
     * @returns {Object} Datos de la aplicación (estructura compatible con DEFAULT_CONFIG).
     */
    static loadData() {
        const data = localStorage.getItem(this.STORAGE_KEY);
        if (data) {
            try {
                const parsed = JSON.parse(data);
                // Fusionar con DEFAULT_CONFIG para garantizar que existan campos nuevos
                // añadidos en versiones posteriores de la aplicación
                const merged = { ...DEFAULT_CONFIG, ...parsed };
                // Asegurar que departamentos e incompatibilidades existan
                if (!merged.departamentos) merged.departamentos = ["General"];
                if (!merged.incompatibilidades) merged.incompatibilidades = {};
                return merged;
            } catch (e) { console.error("Error al parsear datos de localStorage:", e); }
        }
        return { ...DEFAULT_CONFIG };
    }

    /**
     * Guarda los datos actuales de la aplicación en localStorage.
     * Se ejecuta automáticamente en cada cambio de estado.
     * 
     * @param {Object} data - Datos completos de la aplicación a persistir.
     */
    static saveData(data) {
        localStorage.setItem(this.STORAGE_KEY, JSON.stringify(data));
    }

    /**
     * Exporta los datos completos de la aplicación como archivo JSON descargable.
     * Incluye configuración, trabajadores, festivos y todos los metadatos.
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportJson(data) {
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(data, null, 2));
        const a = document.createElement('a'); a.href = dataStr; a.download = `datos_vacaciones_${data.year}.json`; a.click();
    }
}

// ============================================================================
// SERVICIO DE FESTIVOS (API EXTERNA)
// ============================================================================

/**
 * Servicio para obtener festivos oficiales desde la API pública OpenHolidays.
 * Documentación de la API: https://openholidaysapi.org/
 */
class HolidayService {
    /**
     * Obtiene los festivos oficiales de una comunidad autónoma para un año dado.
     * Realiza una petición HTTP a la API de OpenHolidays y transforma el resultado
     * al formato interno "dd/MM/yyyy".
     * 
     * En caso de error de red o respuesta inválida, devuelve un array vacío
     * y registra el error en consola.
     * 
     * @param {string} subdivisionCode - Código ISO de la comunidad (ej. "ES-MD" para Madrid).
     * @param {number} year - Año para el que obtener los festivos.
     * @returns {Promise<string[]>} Array de fechas en formato "dd/MM/yyyy".
     */
    static async fetchHolidays(subdivisionCode, year) {
        try {
            const start = `${year}-01-01`; const end = `${year}-12-31`;
            let url = `https://openholidaysapi.org/PublicHolidays?countryIsoCode=ES&languageIsoCode=ES&validFrom=${start}&validTo=${end}&subdivisionCode=${subdivisionCode}`;
            const res = await fetch(url);
            if (!res.ok) throw new Error("Failed fetch");
            const data = await res.json();
            const festivos = [];
            data.forEach(h => {
                // Convertir formato ISO "YYYY-MM-DD" a formato interno "DD/MM/YYYY"
                const p = h.startDate.split("-");
                festivos.push(`${p[2]}/${p[1]}/${p[0]}`);
            });
            return festivos;
        } catch (e) {
            console.error("Error al obtener festivos de OpenHolidays:", e);
            return [];
        }
    }
}

// ============================================================================
// SERVICIO DE EXPORTACIÓN
// ============================================================================

/**
 * Servicio de exportación de datos a múltiples formatos.
 * Genera archivos descargables: JSON parciales, CSV y documentos PDF
 * (vista mensual y vista Gantt) con diseño profesional.
 */
class ExportService {

    /**
     * Genera y descarga un archivo desde contenido de texto.
     * Añade BOM UTF-8 para compatibilidad con Excel y editores Windows.
     * 
     * @param {string} content - Contenido del archivo.
     * @param {string} fileName - Nombre del archivo de descarga.
     * @param {string} mimeType - Tipo MIME del archivo (ej. 'text/json', 'text/csv').
     */
    static downloadFile(content, fileName, mimeType) {
        const blob = new Blob(["\ufeff" + content], { type: mimeType + ';charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = fileName; a.click();
    }

    /**
     * Exporta la configuración de trabajadores (nombre, días base, días extras) como JSON.
     * No incluye las vacaciones asignadas, solo la estructura de personal.
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportTrabajadoresJson(data) {
        const exp = {};
        Object.keys(data.trabajadores).forEach(k => {
            exp[k] = {
                dias_base: data.trabajadores[k].diasBase || data.trabajadores[k].dias_base || 22,
                dias_extras: data.trabajadores[k].diasExtras || data.trabajadores[k].dias_extras || 0,
                departamento: data.trabajadores[k].departamento || "General"
            };
        });
        this.downloadFile(JSON.stringify(exp, null, 2), `export_config_personal_${data.year}.json`, 'text/json');
    }

    /**
     * Exporta la lista de festivos oficiales como archivo JSON.
     * Las fechas se exportan ordenadas alfabéticamente.
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportFestivosJson(data) {
        this.downloadFile(JSON.stringify([...data.festivos].sort(), null, 2), `export_festivos_${data.year}.json`, 'text/json');
    }

    /**
     * Exporta las vacaciones asignadas a cada trabajador como JSON.
     * Cada entrada contiene la fecha y el año de cupo al que se imputa.
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportVacacionesJson(data) {
        const exp = {};
        Object.keys(data.trabajadores).forEach(k => {
            exp[k] = data.trabajadores[k].vacaciones.map(f => ({ fecha: f, ano_cupo: data.year }));
            exp[k].sort((a, b) => a.fecha.localeCompare(b.fecha));
        });
        this.downloadFile(JSON.stringify(exp, null, 2), `export_vacaciones_${data.year}.json`, 'text/json');
    }

    /**
     * Genera y descarga un PDF con vista mensual de calendarios.
     * Muestra cada mes seleccionado como cuadrícula con:
     * - Fines de semana y festivos marcados en rojo
     * - Días de vacaciones con las iniciales de los trabajadores
     * - Resumen final con el cómputo de días por trabajador
     * 
     * El diseño se adapta automáticamente a la orientación elegida (Portrait/Landscape).
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportToPdfMensual(data, filtroDpto = "") {
        const { jsPDF } = window.jspdf;
        const orientacion = data.orientacion_pdf === "Landscape" ? "landscape" : "portrait";
        const doc = new jsPDF({ orientation: orientacion, unit: 'mm', format: 'a4' });
        const yearStr = data.year.toString();
        const dptoStr = filtroDpto ? ` - Depto: ${filtroDpto}` : "";
        const docTitle = (data.titulo_pagina || "Planificación de Vacaciones") + dptoStr;
        const piePagina = data.pie_pagina_pdf || "Gestor de Vacaciones Pro";
        const isLandscape = orientacion === "landscape";
        const w_page = isLandscape ? 297 : 210;
        const h_page = isLandscape ? 210 : 297;

        // Helper para colores
        const hexToRgbPdf = (hex) => {
            const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
            return result ? [parseInt(result[1], 16), parseInt(result[2], 16), parseInt(result[3], 16)] : [174, 214, 241];
        };

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
            pageDoc.text(`Generado: ${dateStr}`, w_page - 12, 15, { align: 'right' });
            pageDoc.setDrawColor(200, 200, 200);
            pageDoc.setLineWidth(0.3);
            pageDoc.line(12, 18, w_page - 12, 18);
            pageDoc.setFont('helvetica', 'normal');
            pageDoc.setFontSize(8);
            pageDoc.setTextColor(127, 140, 141);
            pageDoc.setDrawColor(220, 220, 220);
            pageDoc.line(12, h_page - 12, w_page - 12, h_page - 12);
            pageDoc.text(piePagina, 12, h_page - 7);
            pageDoc.text(`Página ${pNum}`, w_page - 12, h_page - 7, { align: 'right' });
        }

        drawHeaderFooter(doc, 1);
        let meses = data.meses_a_mostrar || [6, 7, 8, 9];

        // Filtrar meses sin días asignados si la opción está activa
        if (data.ocultar_meses_sin_dias) {
            meses = meses.filter(m => {
                let mTieneDia = false;
                Object.keys(data.trabajadores).forEach(tName => {
                    const t = data.trabajadores[tName];
                    if (filtroDpto && (t.departamento || "General") !== filtroDpto) return;
                    t.vacaciones.forEach(v => {
                        const [, vm, vy] = v.split("/");
                        if (parseInt(vm) === m && parseInt(vy) === data.year) mTieneDia = true;
                    });
                });
                return mTieneDia;
            });
            if (meses.length === 0) meses = data.meses_a_mostrar || [6, 7, 8, 9];
        }

        const daysHeader = ["L", "M", "X", "J", "V", "S", "D"];
        const margin_left = 12;
        const gap_x = 14;
        const gap_y = 12;

        const cols = isLandscape ? 3 : 2;
        const col_width = (w_page - 24 - (gap_x * (cols - 1))) / cols;
        const row_height_blocks = 45;
        let start_y = 26;
        let pNum = 1;
        let y_start_last_row = start_y;

        // Renderizar cada mes como cuadrícula
        meses.forEach((month, index) => {
            const col = index % cols;
            const row = Math.floor((index % (cols * 2)) / cols);

            if (index > 0 && index % (cols * 2) === 0) {
                doc.addPage();
                pNum++;
                drawHeaderFooter(doc, pNum);
            }

            const x_start = margin_left + col * (col_width + gap_x);
            const y_start = start_y + row * (row_height_blocks + gap_y);
            y_start_last_row = y_start;

            doc.setFont('helvetica', 'bold');
            doc.setFontSize(11);
            doc.setTextColor(52, 73, 94);
            const nMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
            doc.text(nMeses[month - 1], x_start + col_width / 2, y_start + 5, { align: 'center' });

            const cell_w = col_width / 7; const cell_h = 6;
            doc.setFont('helvetica', 'bold');
            doc.setFontSize(8.5);
            doc.setTextColor(100, 110, 120);

            let cur_x = x_start; let cur_y = y_start + 8;
            daysHeader.forEach(day => {
                doc.setFillColor(242, 244, 244);
                doc.setTextColor(100, 110, 120);
                doc.rect(cur_x, cur_y, cell_w, cell_h, 'FD');
                doc.text(day, cur_x + cell_w / 2, cur_y + 4.2, { align: 'center' });
                cur_x += cell_w;
            });

            cur_y += cell_h;

            function getMonthWeeks(y, m) {
                const dates = [];
                const d = new Date(y, m - 1, 1);
                while (d.getMonth() === m - 1) { dates.push(d.getDate()); d.setDate(d.getDate() + 1); }
                const offset = (new Date(y, m - 1, 1).getDay() || 7) - 1;
                const weeks = []; let currentWeek = Array(offset).fill(0);
                dates.forEach(day => {
                    currentWeek.push(day);
                    if (currentWeek.length === 7) { weeks.push(currentWeek); currentWeek = []; }
                });
                if (currentWeek.length > 0) {
                    while (currentWeek.length < 7) currentWeek.push(0);
                    weeks.push(currentWeek);
                }
                return weeks;
            }

            const weeks = getMonthWeeks(data.year, month);
            weeks.forEach(week => {
                cur_x = x_start;
                week.forEach((day, dIdx) => {
                    if (day === 0) {
                        doc.setFillColor(255, 255, 255);
                        doc.setDrawColor(200, 200, 200);
                        doc.rect(cur_x, cur_y, cell_w, cell_h, 'S');
                    } else {
                        const dateStr = `${day.toString().padStart(2, '0')}/${month.toString().padStart(2, '0')}/${data.year}`;
                        const esFinDeSemana = (dIdx >= 5);
                        const esFestivoOficial = data.festivos.includes(dateStr);

                        let fillColor = [255, 255, 255]; let textColor = [44, 62, 80];
                        let isFilled = false; let fontStyle = 'normal'; let fontSize = 8.5;
                        let cellText = day.toString();

                        if (esFinDeSemana || esFestivoOficial) {
                            fillColor = [244, 246, 247]; textColor = [231, 76, 60]; isFilled = true;
                        }

                        // Determinar qué trabajadores tienen vacaciones este día
                        const trabsVac = [];
                        for (let tName in data.trabajadores) {
                            if (filtroDpto && (data.trabajadores[tName].departamento || "General") !== filtroDpto) continue;
                            if (data.trabajadores[tName].vacaciones.includes(dateStr)) trabsVac.push(tName);
                        }

                        let tieneCierre = false;
                        let tieneConflicto = false;
                        let hayOtroAno = false;
                        let mainDeptColor = [174, 214, 241]; // Default (azul claro)

                        if (trabsVac.length > 0) {
                            const wMain = data.trabajadores[trabsVac[0]];
                            const dptName = wMain.departamento || "General";
                            mainDeptColor = hexToRgbPdf((data.departamentosColores && data.departamentosColores[dptName]) || '#aed6f1');

                            // Comprobar si hay cierre (recursivo)
                            if (data.cierres) {
                                if (data.cierres["__todos__"] && data.cierres["__todos__"].includes(dateStr)) tieneCierre = true;
                                else {
                                    for (let w of trabsVac) {
                                        let dpt = data.trabajadores[w].departamento || "General";
                                        if (data.cierres[dpt] && data.cierres[dpt].includes(dateStr)) {
                                            tieneCierre = true; break;
                                        }
                                    }
                                }
                            }
                            // Comprobar año cupo
                            for (let w of trabsVac) {
                                const wI = data.trabajadores[w];
                                const yCupo = (wI.imputaciones && wI.imputaciones[dateStr]) || new Date(dateStr.split("/").reverse().join("-")).getFullYear();
                                if (yCupo !== data.year) hayOtroAno = true;
                            }

                            // Comprobar incompatibilidades
                            if (!tieneCierre && data.incompatibilidades) {
                                for (let w of trabsVac) {
                                    let compInc = data.incompatibilidades[w] || [];
                                    // Check dept incomp
                                    let dpt = data.trabajadores[w].departamento || "General";
                                    if (data.cierres && data.cierres[dpt]) {
                                        // in WPF incompatibilities for a department are resolved recursively
                                    }
                                    for (let incomp of compInc) {
                                        if (trabsVac.includes(incomp)) {
                                            tieneConflicto = true; break;
                                        }
                                    }
                                    if (tieneConflicto) break;
                                }
                            }
                        } else {
                            // Check si es cierre sin nadie (solo para pintar si filtramos)
                            const cierresEnFecha = Object.keys(data.cierres || {}).filter(dept => data.cierres[dept].includes(dateStr));
                            const isCierreDept = cierresEnFecha.length > 0 && (!filtroDpto || cierresEnFecha.includes(filtroDpto));
                            if (isCierreDept) {
                                tieneCierre = true;
                                const closureDept = filtroDpto || cierresEnFecha[0];
                                mainDeptColor = hexToRgbPdf((data.departamentosColores && data.departamentosColores[closureDept]) || '#aed6f1');
                            }
                        }

                        // Colorear la celda según el número de trabajadores de vacaciones
                        if (trabsVac.length > 0 || tieneCierre) {
                            if (tieneCierre) {
                                fillColor = [Math.min(mainDeptColor[0] + 50, 255), Math.min(mainDeptColor[1] + 50, 255), Math.min(mainDeptColor[2] + 50, 255)];
                                textColor = [mainDeptColor[0] / 2, mainDeptColor[1] / 2, mainDeptColor[2] / 2];
                            } else if (hayOtroAno) {
                                fillColor = [Math.max(mainDeptColor[0] - 20, 0), Math.max(mainDeptColor[1] - 20, 0), Math.max(mainDeptColor[2] - 20, 0)];
                                textColor = [255, 255, 255];
                            } else {
                                fillColor = mainDeptColor;
                                textColor = [255, 255, 255];
                            }

                            if (tieneConflicto) textColor = [220, 53, 69]; // Rojo para conflictos
                            isFilled = true;

                            let prefix = "";
                            if (tieneCierre) prefix = "C";
                            if (tieneConflicto) prefix += "!";

                            if (trabsVac.length === 0 && tieneCierre) {
                                cellText = `${day}${prefix}`; fontStyle = 'bold'; fontSize = 8;
                            } else if (trabsVac.length === 1) {
                                cellText = `${day}${prefix}(${obtenerIniciales(trabsVac[0])})`; fontStyle = 'bold'; fontSize = 7;
                            } else if (trabsVac.length === 2) {
                                cellText = `${day}${prefix}(${obtenerIniciales(trabsVac[0])},${obtenerIniciales(trabsVac[1])})`; fontStyle = 'bold'; fontSize = 6;
                            } else {
                                cellText = `${day}${prefix}(${obtenerIniciales(trabsVac[0])}+${trabsVac.length - 1})`; fontStyle = 'bold'; fontSize = 6;
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

        // Sección de leyenda y resumen
        let spaceNeeded = 70;
        let endY = y_start_last_row + row_height_blocks + 15;

        if (data.forzar_salto_pagina || (endY + spaceNeeded > h_page - 20)) {
            doc.addPage();
            pNum++;
            drawHeaderFooter(doc, pNum);
            endY = 28;
        } else {
            endY += 5;
        }

        doc.setFont('helvetica', 'bold');
        doc.setFontSize(13);
        doc.setTextColor(44, 62, 80);
        doc.text("Resumen de Vacaciones y Leyenda", 12, endY);

        // Leyenda: Vacaciones libres
        doc.setFillColor(174, 214, 241); doc.setDrawColor(200, 200, 200);
        doc.rect(12, endY + 6, 18, 6, 'FD'); doc.setTextColor(27, 79, 114); doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
        doc.text("Día(XX)", 21, endY + 10.2, { align: 'center' });
        doc.setTextColor(44, 62, 80); doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);
        doc.text("Vacaciones libres (Oscuro: año ant.)", 34, endY + 10.2);

        // Leyenda: Cierre Patronal
        doc.setFillColor(250, 215, 161); doc.rect(12, endY + 15, 18, 6, 'FD'); doc.setTextColor(27, 79, 114); doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
        doc.text("CDía", 21, endY + 19.2, { align: 'center' });
        doc.setTextColor(44, 62, 80); doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);
        doc.text("Cierre Patronal", 34, endY + 19.2);

        // Leyenda: Incompatibilidades
        doc.setFillColor(255, 255, 255); doc.rect(110, endY + 6, 18, 6, 'FD'); doc.setTextColor(192, 57, 43); doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
        doc.text("!Día(XX)", 119, endY + 10.2, { align: 'center' });
        doc.setTextColor(44, 62, 80); doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);
        doc.text("Conflicto por Incompatibilidad", 132, endY + 10.2);

        // Leyenda: festivos y fines de semana
        doc.setFillColor(244, 246, 247); doc.rect(110, endY + 15, 18, 6, 'FD'); doc.setTextColor(231, 76, 60); doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
        doc.text("14", 119, endY + 19.2, { align: 'center' });
        doc.setTextColor(44, 62, 80); doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);
        doc.text("Fines de semana o festivos", 132, endY + 19.2);

        let curDepY = endY + 26;
        let dptosColores = data.departamentosColores || {};
        if (filtroDpto && dptosColores[filtroDpto]) dptosColores = { [filtroDpto]: dptosColores[filtroDpto] };

        if (Object.keys(dptosColores).length > 0) {
            doc.setFont('helvetica', 'bold'); doc.setFontSize(10);
            doc.text("Colores por Departamento:", 12, curDepY);
            let depX = 12;
            curDepY += 5;
            Object.keys(dptosColores).forEach(dept => {
                const color = hexToRgbPdf(dptosColores[dept]);
                doc.setFillColor(color[0], color[1], color[2]);
                doc.rect(depX, curDepY, 4, 4, 'FD');
                doc.setFont('helvetica', 'normal'); doc.setFontSize(9);
                doc.text(dept, depX + 6, curDepY + 3.3);
                depX += 40;
                if (depX > w_page - 40) {
                    depX = 12;
                    curDepY += 6;
                }
            });
            curDepY += 6;
        }

        doc.setDrawColor(220, 220, 220); doc.line(12, curDepY, w_page - 12, curDepY);

        // Cómputo de días consumidos por trabajador
        if (!data.ocultar_computo_gantt) {
            doc.setFont('helvetica', 'bold'); doc.setFontSize(11);
            doc.text("Disfrute de Vacaciones (Días laborables netos consumidos en el año):", 12, curDepY + 7);

            let text_y = curDepY + 14;
            let wNames = Object.keys(data.trabajadores).sort();
            if (filtroDpto) wNames = wNames.filter(w => (data.trabajadores[w].departamento || "General") === filtroDpto);

            wNames.forEach(w => {
                const festivosTrabajador = obtenerFestivosTrabajador(w, data);
                const netos = contarDiasConsumidos(data.trabajadores[w].vacaciones, festivosTrabajador);
                const limite = data.trabajadores[w].dias_base + data.trabajadores[w].dias_extras;
                const excede = netos > limite ? " (Cupo superado!)" : "";
                
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

                const rangosPropias = vPropias.length > 0 ? agruparVacacionesEnTexto(vPropias, festivosTrabajador, data.year) : "Ninguna";
                const rangosCierres = vCierres.length > 0 ? agruparVacacionesEnTexto(vCierres, festivosTrabajador, data.year) : "";

                doc.setFont('helvetica', 'bold'); doc.setTextColor(44, 62, 80);
                doc.text(`- [${obtenerIniciales(w)}] ${w}: ${netos} de ${limite} días consumidos${excede}.`, 15, text_y);
                text_y += 4.5;

                doc.setFont('helvetica', 'italic'); doc.setFontSize(8.5); doc.setTextColor(100, 110, 120);
                const max_w = w_page - 35;
                const linesPropias = doc.splitTextToSize(`Vacaciones libres: ${rangosPropias}`, max_w);
                linesPropias.forEach(line => {
                    if (text_y > h_page - 22) { doc.addPage(); pNum++; drawHeaderFooter(doc, pNum); text_y = 28; }
                    doc.text(line, 20, text_y); text_y += 4.5;
                });
                
                if (rangosCierres) {
                    const linesCierres = doc.splitTextToSize(`Cierres patronales: ${rangosCierres}`, max_w);
                    linesCierres.forEach(line => {
                        if (text_y > h_page - 22) { doc.addPage(); pNum++; drawHeaderFooter(doc, pNum); text_y = 28; }
                        doc.text(line, 20, text_y); text_y += 4.5;
                    });
                }
                
                doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5); doc.setTextColor(44, 62, 80); text_y += 2.0;
                if (text_y > h_page - 22) { doc.addPage(); pNum++; drawHeaderFooter(doc, pNum); text_y = 28; }
            });
        }

        doc.save(`Calendario_Vacaciones_Mensual_${data.year}.pdf`);
    }

    /**
     * Genera y descarga un PDF con vista Gantt (tabla horizontal).
     * Muestra un mes por página con:
     * - Columna de trabajadores a la izquierda
     * - Días del mes como columnas numeradas
     * - Celdas coloreadas para vacaciones, festivos y fines de semana
     * - Cómputo anual al final (opcional según configuración)
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportToPdfGantt(data, filtroDpto = "") {
        const { jsPDF } = window.jspdf;
        const orientacion = data.orientacion_pdf === "Landscape" ? "landscape" : "portrait";
        const doc = new jsPDF({ orientation: orientacion, unit: 'mm', format: 'a4' });
        const yearStr = data.year.toString();
        const dptoStr = filtroDpto ? ` - Depto: ${filtroDpto}` : "";
        const docTitle = (data.titulo_pagina || "Vista Gantt de Vacaciones") + dptoStr;
        const piePagina = data.pie_pagina_pdf || "Gestor de Vacaciones Pro";
        const isLandscape = orientacion === "landscape";
        const w_page = isLandscape ? 297 : 210;
        const h_page = isLandscape ? 210 : 297;

        // Helper para colores
        const hexToRgbPdf = (hex) => {
            const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
            return result ? [parseInt(result[1], 16), parseInt(result[2], 16), parseInt(result[3], 16)] : [174, 214, 241];
        };

        function drawHeaderFooter(pageDoc, pNum) {
            pageDoc.setFont('helvetica', 'bold'); pageDoc.setFontSize(14); pageDoc.setTextColor(44, 62, 80);
            pageDoc.text(`${docTitle} - ${yearStr}`, 12, 15);
            const now = new Date();
            const dateStr = `${now.getDate().toString().padStart(2, '0')}/${(now.getMonth() + 1).toString().padStart(2, '0')}/${now.getFullYear()}`;
            pageDoc.setFont('helvetica', 'italic'); pageDoc.setFontSize(9); pageDoc.setTextColor(100, 110, 120);
            pageDoc.text(`Generado: ${dateStr}`, w_page - 12, 15, { align: 'right' });
            pageDoc.setDrawColor(200, 200, 200); pageDoc.setLineWidth(0.3); pageDoc.line(12, 18, w_page - 12, 18);
            pageDoc.setFont('helvetica', 'normal'); pageDoc.setFontSize(8); pageDoc.setTextColor(127, 140, 141);
            pageDoc.setDrawColor(220, 220, 220); pageDoc.line(12, h_page - 12, w_page - 12, h_page - 12);
            pageDoc.text(piePagina, 12, h_page - 7); pageDoc.text(`Página ${pNum}`, w_page - 12, h_page - 7, { align: 'right' });
        }

        const mesesStr = data.meses_a_mostrar || [6, 7, 8, 9];
        let mesesRango = mesesStr.map(m => ({ year: data.year, month: m - 1 }));

        // Filtrar meses si la opción está activa
        if (data.ocultar_meses_sin_dias) {
            mesesRango = mesesRango.filter(mObj => {
                let hasDay = false;
                Object.keys(data.trabajadores).forEach(tName => {
                    const t = data.trabajadores[tName];
                    if (filtroDpto && (t.departamento || "General") !== filtroDpto) return;
                    t.vacaciones.forEach(v => {
                        const [, vm, vy] = v.split("/");
                        if (parseInt(vm) === mObj.month + 1 && parseInt(vy) === data.year) hasDay = true;
                    });
                });
                return hasDay;
            });
            if (mesesRango.length === 0) mesesRango = mesesStr.map(m => ({ year: data.year, month: m - 1 }));
        }

        let pNum = 1;
        let cur_y = 24;

        // Renderizar meses
        mesesRango.forEach((mObj, idx) => {
            // Filtrar trabajadores y departamentos para este mes para calcular la altura
            let sortedWorkers = Object.keys(data.trabajadores);
            if (filtroDpto) {
                sortedWorkers = sortedWorkers.filter(w => (data.trabajadores[w].departamento || "General") === filtroDpto);
            }
            sortedWorkers.sort();

            let dptosColores = data.departamentosColores || {};
            if (filtroDpto && dptosColores[filtroDpto]) dptosColores = { [filtroDpto]: dptosColores[filtroDpto] };

            let depLines = 1;
            if (Object.keys(dptosColores).length > 0) {
                let depX = 12;
                Object.keys(dptosColores).forEach(dept => {
                    depX += 30;
                    if (depX > w_page - 30) {
                        depX = 12;
                        depLines++;
                    }
                });
            }
            const deptoHeight = Object.keys(dptosColores).length > 0 ? (depLines * 4.5) : 0;
            const mesHeight = 13 + (sortedWorkers.length * 7) + 10 + deptoHeight;

            // Decidir si añadir una página
            if (idx > 0 && (data.forzar_salto_pagina || (cur_y + 10 + mesHeight > h_page - 18))) {
                doc.addPage();
                pNum++;
                drawHeaderFooter(doc, pNum);
                cur_y = 24;
            } else if (idx === 0) {
                drawHeaderFooter(doc, pNum);
                cur_y = 24;
            } else {
                cur_y += 10; // Margen de separación entre meses
            }

            const col_name_width = 38; const ancho_dias = w_page - 24 - col_name_width;
            const num_dias = new Date(mObj.year, mObj.month + 1, 0).getDate();
            const col_day_width = ancho_dias / num_dias;

            // Cabecera del mes
            doc.setFont('helvetica', 'bold'); doc.setFontSize(10); doc.setDrawColor(200, 200, 200);
            doc.setFillColor(220, 225, 230); doc.rect(12, cur_y, col_name_width, 7, 'FD');
            doc.setTextColor(44, 62, 80); doc.text("MES", 12 + col_name_width / 2, cur_y + 4.5, { align: 'center' });

            doc.setFillColor(220, 225, 230); doc.rect(12 + col_name_width, cur_y, ancho_dias, 7, 'FD');
            const nMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
            doc.setTextColor(44, 62, 80); doc.text(`${nMeses[mObj.month].toUpperCase()} ${mObj.year}`, 12 + col_name_width + ancho_dias / 2, cur_y + 4.5, { align: 'center' });

            // Fila de números de día
            cur_y += 7; doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
            doc.setFillColor(240, 242, 245); doc.rect(12, cur_y, col_name_width, 6, 'FD');
            doc.setTextColor(100, 110, 120); doc.text("TRABAJADOR", 14, cur_y + 4.2);

            for (let d = 1; d <= num_dias; d++) {
                const x = 12 + col_name_width + (d - 1) * col_day_width;
                doc.setFillColor(240, 242, 245); doc.rect(x, cur_y, col_day_width, 6, 'FD');
                doc.setTextColor(100, 110, 120); doc.text(d.toString(), x + col_day_width / 2, cur_y + 4.2, { align: 'center' });
            }
            cur_y += 6;

            // Filas por trabajador
            sortedWorkers.forEach(w => {
                const wDept = data.trabajadores[w].departamento || "General";
                const wColor = hexToRgbPdf((data.departamentosColores && data.departamentosColores[wDept]) || '#aed6f1');

                doc.setFont('helvetica', 'normal'); doc.setFontSize(8.5);
                doc.setFillColor(252, 252, 252); doc.setTextColor(44, 62, 80);
                doc.rect(12, cur_y, col_name_width, 7, 'FD'); doc.text(w, 14, cur_y + 4.7);

                const listVacaciones = data.trabajadores[w].vacaciones;
                for (let d = 1; d <= num_dias; d++) {
                    const x = 12 + col_name_width + (d - 1) * col_day_width;
                    const dStr = `${d.toString().padStart(2, '0')}/${(mObj.month + 1).toString().padStart(2, '0')}/${mObj.year}`;
                    const testDate = new Date(mObj.year, mObj.month, d);
                    const esWeekend = (testDate.getDay() === 0 || testDate.getDay() === 6);
                    const festivosTrabajador = obtenerFestivosTrabajador(w, data);
                    const esFestivo = festivosTrabajador.includes(dStr);
                    const esVacacion = listVacaciones.includes(dStr);

                    let tieneCierre = false;
                    let tieneConflicto = false;
                    let esOtroAno = false;

                    // Solo chequear si el dia esta seleccionado para este trabajador
                    if (esVacacion) {
                        const yCupo = (data.trabajadores[w].imputaciones && data.trabajadores[w].imputaciones[dStr]) || new Date(dStr.split("/").reverse().join("-")).getFullYear();
                        if (yCupo !== data.year) esOtroAno = true;

                        if (data.cierres) {
                            if (data.cierres["__todos__"] && data.cierres["__todos__"].includes(dStr)) tieneCierre = true;
                            if (data.cierres[wDept] && data.cierres[wDept].includes(dStr)) tieneCierre = true;
                        }
                        if (!tieneCierre && data.incompatibilidades && data.incompatibilidades[w]) {
                            // En Gantt, solo revisamos incomp de este trabajador
                            for (let incomp of data.incompatibilidades[w]) {
                                if (data.trabajadores[incomp] && data.trabajadores[incomp].vacaciones.includes(dStr)) {
                                    tieneConflicto = true; break;
                                }
                            }
                        }
                    } else {
                        // Comprobar si hay cierre general o de dpto, aunque no lo haya marcado aun
                        if (data.cierres) {
                            if (data.cierres["__todos__"] && data.cierres["__todos__"].includes(dStr)) tieneCierre = true;
                            if (data.cierres[wDept] && data.cierres[wDept].includes(dStr)) tieneCierre = true;
                        }
                    }

                    let fillColor = [255, 255, 255]; let isFilled = false;
                    let cMark = "";

                    if (esVacacion) {
                        if (tieneCierre) {
                            fillColor = [Math.min(wColor[0] + 50, 255), Math.min(wColor[1] + 50, 255), Math.min(wColor[2] + 50, 255)];
                            cMark = tieneConflicto ? "!C" : "C";
                        } else if (tieneConflicto) {
                            fillColor = [Math.max(wColor[0] - 20, 0), Math.max(wColor[1] - 20, 0), Math.max(wColor[2] - 20, 0)];
                        } else {
                            fillColor = wColor;
                        }
                        isFilled = true;
                        if (!tieneCierre && tieneConflicto) cMark = "!";
                    }
                    else if (esFestivo || esWeekend) { fillColor = [235, 237, 239]; isFilled = true; }

                    doc.setFillColor(fillColor[0], fillColor[1], fillColor[2]);
                    doc.rect(x, cur_y, col_day_width, 7, isFilled ? 'FD' : 'S');

                    if (cMark) {
                        if (cMark.includes("!")) doc.setTextColor(220, 53, 69);
                        else doc.setTextColor(44, 62, 80);
                        doc.setFont('helvetica', 'bold'); doc.setFontSize(7.5);
                        doc.text(cMark, x + col_day_width / 2, cur_y + 4.7, { align: 'center' });
                    }
                }
                cur_y += 7;
            });

            // Leyenda al pie de cada mes
            cur_y += 5; doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.setTextColor(100, 110, 120);
            doc.text("■ Vacaciones / Festivos", 12, cur_y);
            doc.text("C Cierre Empresa / Dpto", 60, cur_y);
            doc.text("! Conflicto Incompatibilidad", 110, cur_y);

            cur_y += 5;

            if (Object.keys(dptosColores).length > 0) {
                let depX = 12;
                Object.keys(dptosColores).forEach(dept => {
                    const color = hexToRgbPdf(dptosColores[dept]);
                    doc.setFillColor(color[0], color[1], color[2]);
                    doc.rect(depX, cur_y - 2.5, 3, 3, 'FD');
                    doc.text(dept, depX + 4, cur_y);
                    depX += 30;
                    if (depX > w_page - 30) {
                        depX = 12;
                        cur_y += 4.5;
                    }
                });
                cur_y += 4.5;
            }
        });

        // Cómputo anual de vacaciones (página adicional)
        if (!data.ocultar_computo_gantt) {
            let text_y = cur_y + 15;
            if (data.forzar_salto_pagina || mesesRango.length === 0 || text_y > h_page - 30) {
                doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages());
                text_y = 28;
            }
            doc.setFont('helvetica', 'bold'); doc.setFontSize(12); doc.setTextColor(44, 62, 80);
            doc.text("Cómputo Anual de Vacaciones (Días laborables netos disfrutados):", 12, text_y);
            text_y += 8; doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);

            let sortedWorkers = Object.keys(data.trabajadores).sort();
            if (filtroDpto) sortedWorkers = sortedWorkers.filter(w => (data.trabajadores[w].departamento || "General") === filtroDpto);

            sortedWorkers.forEach(w => {
                const netos = contarDiasConsumidos(data.trabajadores[w].vacaciones, data.festivos);
                const limite = data.trabajadores[w].dias_base + data.trabajadores[w].dias_extras;
                const excede = netos > limite ? " (Cupo superado!)" : "";
                
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

                const rangosPropias = vPropias.length > 0 ? agruparVacacionesEnTexto(vPropias, data.festivos, data.year) : "Ninguna";
                const rangosCierres = vCierres.length > 0 ? agruparVacacionesEnTexto(vCierres, data.festivos, data.year) : "";

                doc.setFont('helvetica', 'bold'); doc.setTextColor(44, 62, 80);
                doc.text(`- ${w}: ${netos} días netos disfrutados de un cupo total de ${limite} días${excede}.`, 15, text_y);
                text_y += 4.5;
                doc.setFont('helvetica', 'italic'); doc.setFontSize(8.5); doc.setTextColor(100, 110, 120);
                const max_width = isLandscape ? 265 : 180;
                const max_height = h_page - 25;
                
                const linesPropias = doc.splitTextToSize(`Vacaciones libres: ${rangosPropias}`, max_width);
                linesPropias.forEach(line => {
                    if (text_y > max_height) { doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages()); text_y = 28; }
                    doc.text(line, 20, text_y); text_y += 4.5;
                });
                
                if (rangosCierres) {
                    const linesCierres = doc.splitTextToSize(`Cierres patronales: ${rangosCierres}`, max_width);
                    linesCierres.forEach(line => {
                        if (text_y > max_height) { doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages()); text_y = 28; }
                        doc.text(line, 20, text_y); text_y += 4.5;
                    });
                }
                
                doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5); doc.setTextColor(44, 62, 80); text_y += 2.0;
                if (text_y > max_height) { doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages()); text_y = 28; }
            });
        }

        doc.save(`Calendario_Vacaciones_Tabla_${data.year}.pdf`);
    }

    /**
     * Exporta la configuración de trabajadores como archivo CSV compatible con Excel.
     * Formato: TRABAJADOR, DIAS_BASE, DIAS_EXTRAS
     * 
     * @param {Object} data - Datos completos de la aplicación.
     */
    static exportToExcel(data) {
        let csv = "TRABAJADOR,DIAS_BASE,DIAS_EXTRAS\n";
        Object.keys(data.trabajadores).forEach(w => {
            const inf = data.trabajadores[w];
            csv += `"${w}",${inf.dias_base},${inf.dias_extras}\n`;
        });
        this.downloadFile(csv, `export_config_personal_${data.year}.csv`, 'text/csv');
    }

    /**
     * Exporta los datos de un trabajador individual como archivo JSON.
     * Incluye toda su información: vacaciones, días base/extras, departamento.
     * 
     * @param {Object} data - Datos completos de la aplicación.
     * @param {string} trabajador - Nombre del trabajador a exportar.
     */
    static exportTrabajadorJson(data, trabajador) {
        if (!data.trabajadores[trabajador]) return;
        const exportObj = {
            [trabajador]: data.trabajadores[trabajador]
        };
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(exportObj, null, 2));
        const a = document.createElement('a'); a.href = dataStr; a.download = `vacaciones_${trabajador}.json`; a.click();
    }
}
