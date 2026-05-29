/**
 * utils.js - Funciones utilitarias del Calendario de Vacaciones
 * 
 * Contiene funciones auxiliares de cálculo, formateo y parseo de datos
 * que son utilizadas por los servicios y componentes de la aplicación.
 * 
 * IMPORTANTE: Este archivo se carga como script global (no ES module)
 * para evitar restricciones de CORS al abrir index.html directamente
 * desde el sistema de archivos (protocolo file://).
 * Todas las funciones se exponen en el ámbito global (window).
 */

// ============================================================================
// CONSTANTES GLOBALES
// ============================================================================

/**
 * Lista de comunidades autónomas de España con sus códigos ISO 3166-2.
 * Se usa para seleccionar la comunidad al importar festivos desde la API OpenHolidays.
 * @type {Array<{id: string, name: string}>}
 */
const COMUNIDADES = [
    { id: "ES-AN", name: "Andalucía" }, { id: "ES-AR", name: "Aragón" }, { id: "ES-AS", name: "Asturias" },
    { id: "ES-CB", name: "Cantabria" }, { id: "ES-CE", name: "Ceuta" }, { id: "ES-CL", name: "Castilla y León" },
    { id: "ES-CM", name: "Castilla-La Mancha" }, { id: "ES-CN", name: "Canarias" }, { id: "ES-CT", name: "Cataluña" },
    { id: "ES-EX", name: "Extremadura" }, { id: "ES-GA", name: "Galicia" }, { id: "ES-IB", name: "Islas Baleares" },
    { id: "ES-MC", name: "Murcia" }, { id: "ES-MD", name: "Comunidad de Madrid" }, { id: "ES-ML", name: "Melilla" },
    { id: "ES-NC", name: "Navarra" }, { id: "ES-PV", name: "País Vasco" }, { id: "ES-RI", name: "La Rioja" },
    { id: "ES-VC", name: "Comunidad Valenciana" }
];

/**
 * Nombres de los meses en castellano, indexados de 0 (Enero) a 11 (Diciembre).
 * Se reutiliza en calendarios, PDFs y resúmenes.
 * @type {string[]}
 */
const NOMBRES_MESES = [
    "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
    "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
];

/**
 * Configuración por defecto de la aplicación.
 * Se aplica como base cuando no hay datos persistidos en localStorage,
 * o cuando se fusionan datos importados con la configuración existente.
 * 
 * Propiedades:
 * - titulo_pagina:        Título mostrado en la barra y en los PDFs exportados.
 * - year:                 Año de cupo activo (por defecto, el año actual).
 * - festivos:             Array de fechas "dd/MM/yyyy" que son festivos oficiales.
 * - trabajadores:         Diccionario {nombre: {vacaciones, dias_base, dias_extras, departamento, imputaciones}}.
 * - departamentos:        Array de nombres de departamentos gestionables.
 * - incompatibilidades:   Diccionario {nombre_trabajador: [nombres_incompatibles...]}.
 * - comunidadAutonoma:    Código ISO de la comunidad para importar festivos (por defecto Madrid).
 * - pie_pagina_pdf:       Texto del pie de página en reportes PDF.
 * - orientacion_pdf:      Orientación del PDF Mensual ("Portrait" o "Landscape").
 * - ocultar_computo_gantt: Si true, no muestra la sección de cómputo en la vista Gantt PDF.
 * - meses_a_mostrar:      Array de números de mes (1-12) visibles en el calendario y PDFs.
 * - ocultar_meses_sin_dias: Si true, oculta meses vacíos en el PDF.
 * - forzar_salto_pagina:  Si true, fuerza salto de página antes del resumen en PDF.
 * - festivosDepartamento:   Diccionario {nombre_departamento: [fechas_festivas...]}.
 * @type {Object}
 */
const DEFAULT_CONFIG = {
    titulo_pagina: "Calendario de Vacaciones",
    year: new Date().getFullYear(),
    festivos: [],
    festivosDepartamento: {},
    trabajadores: {},
    departamentos: ["General"],
    incompatibilidades: {},
    cierres: {},
    departamentos_incompatibles: [],
    departamentos_colores: {},
    comunidadAutonoma: 'ES-MD',
    pie_pagina_pdf: "Gestor de Vacaciones Pro",
    orientacion_pdf: "Portrait",
    ocultar_computo_gantt: false,
    meses_a_mostrar: [6, 7, 8, 9],
    ocultar_meses_sin_dias: false,
    forzar_salto_pagina: true
};

// ============================================================================
// FUNCIONES DE UTILIDAD
// ============================================================================

function obtenerFestivosTrabajador(workerName, data) {
    let f = [...(data.festivos || [])];
    if (data.trabajadores[workerName]) {
        const dept = data.trabajadores[workerName].departamento || "General";
        if (data.festivosDepartamento && data.festivosDepartamento[dept]) {
            f = [...f, ...data.festivosDepartamento[dept]];
        }
    }
    return [...new Set(f)];
}

/**
 * Obtiene las iniciales de un nombre completo.
 * Si el nombre tiene dos o más palabras, devuelve la primera letra de las dos primeras.
 * Si solo tiene una palabra, devuelve las dos primeras letras.
 * Ejemplo: "Juan Pérez" → "JP", "Admin" → "AD"
 * 
 * @param {string} nombre - Nombre completo del trabajador.
 * @returns {string} Iniciales en mayúsculas (máximo 2 caracteres).
 */
function obtenerIniciales(nombre) {
    const partes = nombre.trim().split(/\s+/);
    if (partes.length >= 2) return (partes[0][0] + partes[1][0]).toUpperCase();
    else if (partes.length === 1 && partes[0]) return partes[0].substring(0, 2).toUpperCase();
    return '';
}

/**
 * Calcula la estructura de semanas de un mes dado, con lunes como primer día.
 * Cada semana es un array de 7 posiciones (Lun=0..Dom=6).
 * Los días fuera del mes se representan como 0.
 * 
 * Ejemplo para un mes que empieza en miércoles:
 *   [0, 0, 1, 2, 3, 4, 5]   ← primera semana (L y M vacíos)
 *   [6, 7, 8, 9, 10, 11, 12]
 *   ...
 * 
 * @param {number} year - Año (ej. 2026).
 * @param {number} month - Mes (1-12, donde 1=Enero).
 * @returns {Array<number[]>} Array de semanas, cada una con 7 posiciones.
 */
function getMonthWeeks(year, month) {
    const weeks = [];
    const firstDay = new Date(year, month - 1, 1);
    const lastDay = new Date(year, month, 0);
    let dayOfWeek = firstDay.getDay();
    // Convertir de domingo=0 a lunes=0 (formato europeo)
    let startOffset = (dayOfWeek === 0) ? 6 : dayOfWeek - 1;
    let currentWeek = Array(7).fill(0);
    let dayCounter = 1;
    for (let i = startOffset; i < 7; i++) currentWeek[i] = dayCounter++;
    weeks.push(currentWeek);
    while (dayCounter <= lastDay.getDate()) {
        currentWeek = Array(7).fill(0);
        for (let i = 0; i < 7 && dayCounter <= lastDay.getDate(); i++) currentWeek[i] = dayCounter++;
        weeks.push(currentWeek);
    }
    return weeks;
}

/**
 * Agrupa un conjunto de fechas de vacaciones en rangos legibles en castellano.
 * Tiene en cuenta fines de semana y festivos para considerar rangos continuos.
 * Ejemplo: ["01/07/2026", "02/07/2026", "03/07/2026"] → "del 1 al 3 de Julio"
 * Si hay saltos (días laborables entre medias), los separa con comas.
 * 
 * @param {string[]} fechas - Array de fechas en formato "dd/MM/yyyy".
 * @param {string[]} festivos - Array de festivos oficiales en formato "dd/MM/yyyy".
 * @param {number} year - Año de referencia.
 * @returns {string} Texto descriptivo de los rangos de vacaciones.
 */
function agruparVacacionesEnTexto(fechas, festivos, year) {
    if (!fechas || fechas.length === 0) return "Sin vacaciones asignadas";
    // Parsear y ordenar cronológicamente
    const list = fechas.map(f => {
        const p = f.split("/");
        return { str: f, date: new Date(parseInt(p[2]), parseInt(p[1]) - 1, parseInt(p[0])) };
    }).sort((a, b) => a.date - b.date);

    // Agrupar en rangos continuos (saltando fines de semana y festivos)
    const ranges = [];
    let currentRange = [list[0]];
    for (let i = 1; i < list.length; i++) {
        const prevItem = currentRange[currentRange.length - 1];
        const currItem = list[i];
        let esContinuo = true;
        let tempDate = new Date(prevItem.date);
        tempDate.setDate(tempDate.getDate() + 1);
        // Verificar que los días intermedios son todos no laborables
        while (tempDate < currItem.date) {
            const tempStr = `${tempDate.getDate().toString().padStart(2, '0')}/${(tempDate.getMonth() + 1).toString().padStart(2, '0')}/${tempDate.getFullYear()}`;
            const dayOfWeek = tempDate.getDay();
            const esFinSemana = (dayOfWeek === 0 || dayOfWeek === 6);
            if (!esFinSemana && !festivos.includes(tempStr)) {
                esContinuo = false;
                break;
            }
            tempDate.setDate(tempDate.getDate() + 1);
        }
        if (esContinuo) currentRange.push(currItem);
        else { ranges.push(currentRange); currentRange = [currItem]; }
    }
    ranges.push(currentRange);

    // Generar texto legible para cada rango
    const rangesText = ranges.map(range => {
        const start = range[0].date; const end = range[range.length - 1].date;
        if (start.getTime() === end.getTime()) return `el ${start.getDate()} de ${NOMBRES_MESES[start.getMonth()]}`;
        if (start.getMonth() === end.getMonth()) return `del ${start.getDate()} al ${end.getDate()} de ${NOMBRES_MESES[start.getMonth()]}`;
        return `del ${start.getDate()} de ${NOMBRES_MESES[start.getMonth()]} al ${end.getDate()} de ${NOMBRES_MESES[end.getMonth()]}`;
    });
    if (rangesText.length === 1) return rangesText[0];
    const lastText = rangesText.pop();
    return rangesText.join(", ") + " y " + lastText;
}

/**
 * Cuenta los días laborables netos consumidos de un conjunto de fechas de vacaciones.
 * Excluye fines de semana (sábado y domingo) y festivos oficiales del cómputo.
 * 
 * @param {string[]} vacaciones - Array de fechas "dd/MM/yyyy" marcadas como vacaciones.
 * @param {string[]} festivos - Array de festivos oficiales "dd/MM/yyyy".
 * @returns {number} Número de días laborables netos consumidos.
 */
function contarDiasConsumidos(vacaciones, festivos) {
    let dias = 0;
    vacaciones.forEach(dateStr => {
        const parts = dateStr.split("/");
        const date = new Date(parseInt(parts[2]), parseInt(parts[1]) - 1, parseInt(parts[0]));
        const dayOfWeek = date.getDay();
        if (dayOfWeek !== 0 && dayOfWeek !== 6 && !festivos.includes(dateStr)) dias++;
    });
    return dias;
}

/**
 * Parsea una línea de texto CSV respetando comillas dobles.
 * Las comas dentro de comillas no separan campos.
 * Ejemplo: '"Juan García",22,3' → ["Juan García", "22", "3"]
 * 
 * @param {string} line - Una línea de texto CSV.
 * @returns {string[]} Array de campos extraídos (con trim aplicado).
 */
function parseCSVLine(line) {
    const row = []; let inQuotes = false; let currentToken = "";
    for (let i = 0; i < line.length; i++) {
        let char = line[i];
        if (char === '"') inQuotes = !inQuotes;
        else if (char === ',' && !inQuotes) { row.push(currentToken.trim()); currentToken = ""; }
        else currentToken += char;
    }
    row.push(currentToken.trim()); return row;
}

/**
 * Importa datos de forma inteligente desde texto (JSON o CSV) y los fusiona
 * con los datos actuales de la aplicación.
 * 
 * Detecta automáticamente el tipo de datos basándose en su estructura:
 * - JSON consolidado (con claves "trabajadores", "festivos", "titulo_pagina"): fusión completa
 * - JSON array de fechas: se añaden como festivos
 * - JSON de objetos con "dias_base"/"dias_extras": se importan como configuración de trabajadores
 * - JSON de arrays con fechas o {fecha, ano_cupo}: se importan como vacaciones asignadas
 * - CSV de una columna con fechas: festivos
 * - CSV de varias columnas numéricas: configuración de trabajadores
 * - CSV con fechas tras el nombre: vacaciones asignadas
 * 
 * @param {Object} currentData - Datos actuales de la aplicación (estructura DEFAULT_CONFIG).
 * @param {string} text - Texto del archivo importado.
 * @param {boolean} isJson - true si el archivo es JSON, false si es CSV.
 * @returns {Object} Datos actualizados tras la fusión.
 */
function importarDataInteligente(currentData, text, isJson) {
    let data = { ...currentData };
    if (isJson) {
        const p = JSON.parse(text);
        if (p.trabajadores || p.festivos || p.titulo_pagina) {
            // JSON consolidado completo: fusionar con los datos actuales
            return { ...data, ...p };
        } else if (Array.isArray(p)) {
            // Array de festivos
            const fests = p.filter(f => typeof f === 'string' && /^\d{2}\/\d{2}\/\d{4}$/.test(f));
            data.festivos = [...new Set([...data.festivos, ...fests])];
            return data;
        } else {
            // Objeto con trabajadores (configuración o vacaciones)
            Object.keys(p).forEach(k => {
                if (typeof p[k] === 'object' && ('dias_base' in p[k] || 'dias_extras' in p[k])) {
                    // Importar configuración de trabajador (días base/extras)
                    data.trabajadores[k] = { ...(data.trabajadores[k] || { vacaciones: [], dias_base: 22, dias_extras: 0, departamento: "General", imputaciones: {} }), ...p[k] };
                } else if (Array.isArray(p[k])) {
                    // Importar vacaciones asignadas
                    const fests = [];
                    p[k].forEach(item => {
                        if (typeof item === 'string' && /^\d{2}\/\d{2}\/\d{4}$/.test(item)) fests.push(item);
                        else if (item && item.fecha && /^\d{2}\/\d{2}\/\d{4}$/.test(item.fecha)) fests.push(item.fecha);
                    });
                    data.trabajadores[k] = { ...(data.trabajadores[k] || { vacaciones: [], dias_base: 22, dias_extras: 0, departamento: "General", imputaciones: {} }), vacaciones: fests };
                }
            });
            return data;
        }
    } else {
        // Importación desde CSV
        const lines = text.split(/\r?\n/).filter(x => x.trim());
        if (!lines.length) return data;
        const filas = lines.map(parseCSVLine);
        if (filas[0].length === 1 && /^\d{2}\/\d{2}\/\d{4}$/.test(filas[0][0])) {
            // CSV de festivos (una fecha por línea)
            const fests = filas.map(r => r[0]);
            data.festivos = [...new Set([...data.festivos, ...fests])];
        } else if (filas[0].length >= 2 && !isNaN(parseInt(filas[0][1]))) {
            // CSV de configuración de trabajadores (nombre, dias_base, dias_extras)
            filas.forEach(row => {
                const name = row[0]; if (!name) return;
                const db = parseInt(row[1]) || 22; const de = parseInt(row[2]) || 0;
                data.trabajadores[name] = { ...(data.trabajadores[name] || { vacaciones: [], departamento: "General", imputaciones: {} }), dias_base: db, dias_extras: de };
            });
        } else {
            // CSV de vacaciones asignadas (nombre, fecha1, fecha2, ...)
            filas.forEach(row => {
                const name = row[0]; if (!name) return;
                const fests = [];
                for (let i = 1; i < row.length; i++) {
                    let d = row[i];
                    if (d.includes(':')) d = d.split(':')[0]; // Eliminar sufijo de año de cupo
                    if (/^\d{2}\/\d{2}\/\d{4}$/.test(d)) fests.push(d);
                }
                data.trabajadores[name] = { ...(data.trabajadores[name] || { vacaciones: [], dias_base: 22, dias_extras: 0, departamento: "General", imputaciones: {} }), vacaciones: fests };
            });
        }
    }
    return data;
}

/**
 * Comprueba las incompatibilidades de vacaciones para un trabajador en una fecha dada.
 * Busca si alguno de los trabajadores incompatibles ya tiene vacaciones ese día.
 * 
 * @param {string} nombreTrabajador - Nombre del trabajador que se está comprobando.
 * @param {string} fecha - Fecha en formato "dd/MM/yyyy" a verificar.
 * @param {Object} trabajadores - Diccionario de todos los trabajadores.
 * @param {Object} incompatibilidades - Diccionario de reglas de incompatibilidad.
 * @param {Object} cierres - Diccionario de cierres por departamento.
 * @returns {string[]} Array con los nombres de trabajadores incompatibles que ya tienen ese día de vacaciones.
 *                      Array vacío si no hay conflictos.
 */
function comprobarIncompatibilidades(nombreTrabajador, fecha, trabajadores, incompatibilidades, cierres = {}) {
    const reglas = incompatibilidades[nombreTrabajador];
    if (!reglas || reglas.length === 0) return [];
    
    const worker = trabajadores[nombreTrabajador];
    const dept = worker ? (worker.departamento || "General") : "General";
    
    // Si la fecha es un cierre para este trabajador, ignoramos incompatibilidades
    if (cierres["__todos__"] && cierres["__todos__"].includes(fecha)) return [];
    if (cierres[dept] && cierres[dept].includes(fecha)) return [];

    const conflictos = [];
    reglas.forEach(incomp => {
        const otherWorker = trabajadores[incomp];
        if (otherWorker && otherWorker.vacaciones.includes(fecha)) {
            // Comprobar si también es un cierre para el otro trabajador
            const otherDept = otherWorker.departamento || "General";
            const isOtherClosure = (cierres["__todos__"] && cierres["__todos__"].includes(fecha)) || 
                                   (cierres[otherDept] && cierres[otherDept].includes(fecha));
            if (!isOtherClosure) {
                conflictos.push(incomp);
            }
        }
    });
    return conflictos;
}

/**
 * Obtiene todos los conflictos de incompatibilidad activos para un trabajador,
 * revisando todas sus fechas de vacaciones asignadas.
 * 
 * @param {string} nombreTrabajador - Nombre del trabajador.
 * @param {Object} trabajadores - Diccionario de todos los trabajadores.
 * @param {Object} incompatibilidades - Diccionario de reglas de incompatibilidad.
 * @param {Object} cierres - Diccionario de cierres.
 * @returns {Array<{fecha: string, conflictos: string[]}>} Array de objetos con la fecha y los nombres en conflicto.
 */
function obtenerTodosLosConflictos(nombreTrabajador, trabajadores, incompatibilidades, cierres = {}) {
    const worker = trabajadores[nombreTrabajador];
    if (!worker) return [];
    const resultado = [];
    worker.vacaciones.forEach(fecha => {
        const conflictos = comprobarIncompatibilidades(nombreTrabajador, fecha, trabajadores, incompatibilidades, cierres);
        if (conflictos.length > 0) {
            resultado.push({ fecha, conflictos });
        }
    });
    return resultado;
}
