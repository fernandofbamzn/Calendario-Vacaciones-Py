const html = htm.bind(React.createElement);
const { useState, useEffect, useMemo, useRef, Fragment } = React;

const COMUNIDADES = [
    { id: "ES-AN", name: "Andalucía" }, { id: "ES-AR", name: "Aragón" }, { id: "ES-AS", name: "Asturias" },
    { id: "ES-CB", name: "Cantabria" }, { id: "ES-CE", name: "Ceuta" }, { id: "ES-CL", name: "Castilla y León" },
    { id: "ES-CM", name: "Castilla-La Mancha" }, { id: "ES-CN", name: "Canarias" }, { id: "ES-CT", name: "Cataluña" },
    { id: "ES-EX", name: "Extremadura" }, { id: "ES-GA", name: "Galicia" }, { id: "ES-IB", name: "Islas Baleares" },
    { id: "ES-MC", name: "Murcia" }, { id: "ES-MD", name: "Comunidad de Madrid" }, { id: "ES-ML", name: "Melilla" },
    { id: "ES-NC", name: "Navarra" }, { id: "ES-PV", name: "País Vasco" }, { id: "ES-RI", name: "La Rioja" },
    { id: "ES-VC", name: "Comunidad Valenciana" }
];

const DEFAULT_CONFIG = {
    titulo_pagina: "Planificación de Vacaciones",
    year: new Date().getFullYear(),
    festivos: [],
    trabajadores: {},
    comunidadAutonoma: 'ES-MD',
    pie_pagina_pdf: "Gestor de Vacaciones Pro",
    orientacion_pdf: "Portrait",
    ocultar_computo_gantt: false,
    meses_a_mostrar: [6, 7, 8, 9],
    ocultar_meses_sin_dias: false,
    forzar_salto_pagina: true
};

class StorageService {
    static STORAGE_KEY = 'CalendarioVacacionesData';
    static loadData() {
        const data = localStorage.getItem(this.STORAGE_KEY);
        if (data) {
            try {
                const parsed = JSON.parse(data);
                return { ...DEFAULT_CONFIG, ...parsed };
            } catch (e) { console.error(e); }
        }
        return { ...DEFAULT_CONFIG };
    }
    static saveData(data) {
        localStorage.setItem(this.STORAGE_KEY, JSON.stringify(data));
    }
    static exportJson(data) {
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(data, null, 2));
        const a = document.createElement('a'); a.href = dataStr; a.download = `datos_vacaciones_${data.year}.json`; a.click();
    }
}

class HolidayService {
    static async fetchHolidays(subdivisionCode, year) {
        try {
            const start = `${year}-01-01`; const end = `${year}-12-31`;
            let url = `https://openholidaysapi.org/PublicHolidays?countryIsoCode=ES&languageIsoCode=ES&validFrom=${start}&validTo=${end}&subdivisionCode=${subdivisionCode}`;
            const res = await fetch(url);
            if (!res.ok) throw new Error("Failed fetch");
            const data = await res.json();
            const festivos = [];
            data.forEach(h => {
                const p = h.startDate.split("-");
                festivos.push(`${p[2]}/${p[1]}/${p[0]}`);
            });
            return festivos;
        } catch (e) {
            console.error(e);
            return [];
        }
    }
}


function obtenerIniciales(nombre) {
    const partes = nombre.trim().split(/\s+/);
    if (partes.length >= 2) return (partes[0][0] + partes[1][0]).toUpperCase();
    else if (partes.length === 1 && partes[0]) return partes[0].substring(0, 2).toUpperCase();
    return '';
}

function getMonthWeeks(year, month) {
    const weeks = [];
    const firstDay = new Date(year, month - 1, 1);
    const lastDay = new Date(year, month, 0);
    let dayOfWeek = firstDay.getDay();
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

function agruparVacacionesEnTexto(fechas, festivos, year) {
    if (!fechas || fechas.length === 0) return "Sin vacaciones asignadas";
    const list = fechas.map(f => {
        const p = f.split("/");
        return { str: f, date: new Date(parseInt(p[2]), parseInt(p[1]) - 1, parseInt(p[0])) };
    }).sort((a, b) => a.date - b.date);
    const ranges = [];
    let currentRange = [list[0]];
    for (let i = 1; i < list.length; i++) {
        const prevItem = currentRange[currentRange.length - 1];
        const currItem = list[i];
        let esContinuo = true;
        let tempDate = new Date(prevItem.date);
        tempDate.setDate(tempDate.getDate() + 1);
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
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    const rangesText = ranges.map(range => {
        const start = range[0].date; const end = range[range.length - 1].date;
        if (start.getTime() === end.getTime()) return `el ${start.getDate()} de ${nombresMeses[start.getMonth()]}`;
        if (start.getMonth() === end.getMonth()) return `del ${start.getDate()} al ${end.getDate()} de ${nombresMeses[start.getMonth()]}`;
        return `del ${start.getDate()} de ${nombresMeses[start.getMonth()]} al ${end.getDate()} de ${nombresMeses[end.getMonth()]}`;
    });
    if (rangesText.length === 1) return rangesText[0];
    const lastText = rangesText.pop();
    return rangesText.join(", ") + " y " + lastText;
}

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

class ExportService {
    static downloadFile(content, fileName, mimeType) {
        const blob = new Blob(["\ufeff" + content], { type: mimeType + ';charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a'); a.href = url; a.download = fileName; a.click();
    }

    static exportTrabajadoresJson(data) {
        const exp = {};
        Object.keys(data.trabajadores).forEach(k => {
            exp[k] = { dias_base: data.trabajadores[k].dias_base, dias_extras: data.trabajadores[k].dias_extras };
        });
        this.downloadFile(JSON.stringify(exp, null, 2), `export_config_personal_${data.year}.json`, 'text/json');
    }

    static exportFestivosJson(data) {
        this.downloadFile(JSON.stringify([...data.festivos].sort(), null, 2), `export_festivos_${data.year}.json`, 'text/json');
    }

    static exportVacacionesJson(data) {
        const exp = {};
        Object.keys(data.trabajadores).forEach(k => {
            exp[k] = data.trabajadores[k].vacaciones.map(f => ({ fecha: f, ano_cupo: data.year }));
            exp[k].sort((a, b) => a.fecha.localeCompare(b.fecha));
        });
        this.downloadFile(JSON.stringify(exp, null, 2), `export_vacaciones_${data.year}.json`, 'text/json');
    }

    static exportToPdfMensual(data) {
        const { jsPDF } = window.jspdf;
        const orientacion = data.orientacion_pdf === "Landscape" ? "landscape" : "portrait";
        const doc = new jsPDF({ orientation: orientacion, unit: 'mm', format: 'a4' });
        const yearStr = data.year.toString();
        const docTitle = data.titulo_pagina || "Planificación de Vacaciones";
        const piePagina = data.pie_pagina_pdf || "Gestor de Vacaciones Pro";
        const isLandscape = orientacion === "landscape";
        const w_page = isLandscape ? 297 : 210;
        const h_page = isLandscape ? 210 : 297;

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

        if (data.ocultar_meses_sin_dias) {
            meses = meses.filter(m => {
                let mTieneDia = false;
                Object.values(data.trabajadores).forEach(t => {
                    t.vacaciones.forEach(v => {
                        const [, vm, vy] = v.split("/");
                        if (parseInt(vm) === m && parseInt(vy) === data.year) mTieneDia = true;
                    });
                });
                return mTieneDia;
            });
            if (meses.length === 0) meses = data.meses_a_mostrar || [6, 7, 8, 9];
        }

        const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
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
            doc.text(nombresMeses[month - 1], x_start + col_width / 2, y_start + 5, { align: 'center' });

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

                        const trabsVac = [];
                        for (let tName in data.trabajadores) {
                            if (data.trabajadores[tName].vacaciones.includes(dateStr)) trabsVac.push(tName);
                        }

                        if (trabsVac.length > 0) {
                            fillColor = [174, 214, 241]; textColor = [27, 79, 114]; isFilled = true;
                            if (trabsVac.length === 1) {
                                cellText = `${day}(${obtenerIniciales(trabsVac[0])})`; fontStyle = 'bold'; fontSize = 7;
                            } else if (trabsVac.length === 2) {
                                cellText = `${day}(${obtenerIniciales(trabsVac[0])},${obtenerIniciales(trabsVac[1])})`; fontStyle = 'bold'; fontSize = 6;
                            } else {
                                cellText = `${day}(${obtenerIniciales(trabsVac[0])}+${trabsVac.length - 1})`; fontStyle = 'bold'; fontSize = 6;
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

        // Determine if we need a new page for the legend
        let spaceNeeded = 70; // approximate space for legend and some lines
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

        doc.setFillColor(174, 214, 241); doc.setDrawColor(200, 200, 200);
        doc.rect(12, endY + 6, 18, 6, 'FD'); doc.setTextColor(27, 79, 114); doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
        doc.text("Día(XX)", 21, endY + 10.2, { align: 'center' });
        doc.setTextColor(44, 62, 80); doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);
        doc.text("Días de vacaciones disfrutadas por el personal (Iniciales del empleado)", 34, endY + 10.2);

        doc.setFillColor(244, 246, 247); doc.rect(12, endY + 15, 18, 6, 'FD'); doc.setTextColor(231, 76, 60); doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
        doc.text("14", 21, endY + 19.2, { align: 'center' });
        doc.setTextColor(44, 62, 80); doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);
        doc.text("Fines de semana o días festivos oficiales", 34, endY + 19.2);

        doc.setDrawColor(220, 220, 220); doc.line(12, endY + 26, w_page - 12, endY + 26);

        if (!data.ocultar_computo_gantt) {
            doc.setFont('helvetica', 'bold'); doc.setFontSize(11);
            doc.text("Disfrute de Vacaciones (Días laborables netos consumidos en el año):", 12, endY + 33);

            let text_y = endY + 40;
            const wNames = Object.keys(data.trabajadores).sort();
            wNames.forEach(w => {
                const netos = contarDiasConsumidos(data.trabajadores[w].vacaciones, data.festivos);
                const limite = data.trabajadores[w].dias_base + data.trabajadores[w].dias_extras;
                const excede = netos > limite ? " (Cupo superado!)" : "";
                const rangosTexto = agruparVacacionesEnTexto(data.trabajadores[w].vacaciones, data.festivos, data.year);

                doc.setFont('helvetica', 'bold'); doc.setTextColor(44, 62, 80);
                doc.text(`- [${obtenerIniciales(w)}] ${w}: ${netos} de ${limite} días consumidos${excede}.`, 15, text_y);
                text_y += 4.5;

                doc.setFont('helvetica', 'italic'); doc.setFontSize(8.5); doc.setTextColor(100, 110, 120);
                const max_w = w_page - 35;
                const lines = doc.splitTextToSize(`Vacaciones: ${rangosTexto}`, max_w);
                lines.forEach(line => {
                    if (text_y > h_page - 22) { doc.addPage(); pNum++; drawHeaderFooter(doc, pNum); text_y = 28; }
                    doc.text(line, 20, text_y); text_y += 4.5;
                });
                doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5); doc.setTextColor(44, 62, 80); text_y += 2.0;
                if (text_y > h_page - 22) { doc.addPage(); pNum++; drawHeaderFooter(doc, pNum); text_y = 28; }
            });
        }

        doc.save(`Calendario_Vacaciones_Mensual_${data.year}.pdf`);
    }

    static exportToPdfGantt(data) {
        const { jsPDF } = window.jspdf;
        const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
        const yearStr = data.year.toString();
        const docTitle = data.titulo_pagina || "Planificación de Vacaciones";
        const piePagina = data.pie_pagina_pdf || "Gestor de Vacaciones Pro";

        function drawHeaderFooter(pageDoc, pNum) {
            pageDoc.setFont('helvetica', 'bold'); pageDoc.setFontSize(14); pageDoc.setTextColor(44, 62, 80);
            pageDoc.text(`${docTitle} - ${yearStr}`, 12, 15);
            const now = new Date();
            const dateStr = `${now.getDate().toString().padStart(2, '0')}/${(now.getMonth() + 1).toString().padStart(2, '0')}/${now.getFullYear()}`;
            pageDoc.setFont('helvetica', 'italic'); pageDoc.setFontSize(9); pageDoc.setTextColor(100, 110, 120);
            pageDoc.text(`Generado: ${dateStr}`, 285, 15, { align: 'right' });
            pageDoc.setDrawColor(200, 200, 200); pageDoc.setLineWidth(0.3); pageDoc.line(12, 18, 285, 18);
            pageDoc.setFont('helvetica', 'normal'); pageDoc.setFontSize(8); pageDoc.setTextColor(127, 140, 141);
            pageDoc.setDrawColor(220, 220, 220); pageDoc.line(12, 198, 285, 198);
            pageDoc.text(piePagina, 12, 203); pageDoc.text(`Página ${pNum}`, 285, 203, { align: 'right' });
        }

        const mesesStr = data.meses_a_mostrar || [6, 7, 8, 9];
        const mesesRango = mesesStr.map(m => ({ year: data.year, month: m - 1 }));

        const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

        mesesRango.forEach((mObj, idx) => {
            if (idx > 0) doc.addPage();
            drawHeaderFooter(doc, idx + 1);

            const col_name_width = 38; const ancho_dias = 273 - col_name_width;
            const num_dias = new Date(mObj.year, mObj.month + 1, 0).getDate();
            const col_day_width = ancho_dias / num_dias;

            doc.setFont('helvetica', 'bold'); doc.setFontSize(10); doc.setDrawColor(200, 200, 200);
            doc.setFillColor(220, 225, 230); doc.rect(12, 24, col_name_width, 7, 'FD');
            doc.setTextColor(44, 62, 80); doc.text("MES", 12 + col_name_width / 2, 28.5, { align: 'center' });

            doc.setFillColor(220, 225, 230); doc.rect(12 + col_name_width, 24, ancho_dias, 7, 'FD');
            doc.setTextColor(44, 62, 80); doc.text(`${nombresMeses[mObj.month].toUpperCase()} ${mObj.year}`, 12 + col_name_width + ancho_dias / 2, 28.5, { align: 'center' });

            let cur_y = 31; doc.setFont('helvetica', 'bold'); doc.setFontSize(8);
            doc.setFillColor(240, 242, 245); doc.rect(12, cur_y, col_name_width, 6, 'FD');
            doc.setTextColor(100, 110, 120); doc.text("TRABAJADOR", 14, cur_y + 4.2);

            for (let d = 1; d <= num_dias; d++) {
                const x = 12 + col_name_width + (d - 1) * col_day_width;
                doc.setFillColor(240, 242, 245); doc.rect(x, cur_y, col_day_width, 6, 'FD');
                doc.setTextColor(100, 110, 120); doc.text(d.toString(), x + col_day_width / 2, cur_y + 4.2, { align: 'center' });
            }
            cur_y += 6;

            const sortedWorkers = Object.keys(data.trabajadores).sort();
            sortedWorkers.forEach(w => {
                doc.setFont('helvetica', 'normal'); doc.setFontSize(8.5);
                doc.setFillColor(252, 252, 252); doc.setTextColor(44, 62, 80);
                doc.rect(12, cur_y, col_name_width, 7, 'FD'); doc.text(w, 14, cur_y + 4.7);

                const listVacaciones = data.trabajadores[w].vacaciones;
                for (let d = 1; d <= num_dias; d++) {
                    const x = 12 + col_name_width + (d - 1) * col_day_width;
                    const dStr = `${d.toString().padStart(2, '0')}/${(mObj.month + 1).toString().padStart(2, '0')}/${mObj.year}`;
                    const testDate = new Date(mObj.year, mObj.month, d);
                    const esWeekend = (testDate.getDay() === 0 || testDate.getDay() === 6);
                    const esFestivo = data.festivos.includes(dStr);
                    const esVacacion = listVacaciones.includes(dStr);

                    let fillColor = [255, 255, 255]; let isFilled = false;
                    if (esVacacion) { fillColor = [174, 214, 241]; isFilled = true; }
                    else if (esFestivo || esWeekend) { fillColor = [235, 237, 239]; isFilled = true; }
                    doc.setFillColor(fillColor[0], fillColor[1], fillColor[2]);
                    doc.rect(x, cur_y, col_day_width, 7, isFilled ? 'FD' : 'S');
                }
                cur_y += 7;
            });

            cur_y += 5; doc.setFont('helvetica', 'normal'); doc.setFontSize(8); doc.setTextColor(100, 110, 120);
            doc.setFillColor(174, 214, 241); doc.rect(12, cur_y, 6, 4, 'FD'); doc.text("Vacaciones del personal", 20, cur_y + 3);
            doc.setFillColor(235, 237, 239); doc.rect(62, cur_y, 6, 4, 'FD'); doc.text("Fin de semana / Festivos", 70, cur_y + 3);
        });

        if (!data.ocultar_computo_gantt) {
            if (data.forzar_salto_pagina || mesesRango.length === 0) {
                doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages());
            }
            doc.setFont('helvetica', 'bold'); doc.setFontSize(12); doc.setTextColor(44, 62, 80);
            doc.text("Cómputo Anual de Vacaciones (Días laborables netos disfrutados):", 12, 28);
            let text_y = 36; doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5);

            const sortedWorkers = Object.keys(data.trabajadores).sort();
            sortedWorkers.forEach(w => {
                const netos = contarDiasConsumidos(data.trabajadores[w].vacaciones, data.festivos);
                const limite = data.trabajadores[w].dias_base + data.trabajadores[w].dias_extras;
                const excede = netos > limite ? " (Cupo superado!)" : "";
                const rangosTexto = agruparVacacionesEnTexto(data.trabajadores[w].vacaciones, data.festivos, data.year);

                doc.setFont('helvetica', 'bold'); doc.setTextColor(44, 62, 80);
                doc.text(`- ${w}: ${netos} días netos disfrutados de un cupo total de ${limite} días${excede}.`, 15, text_y);
                text_y += 4.5;
                doc.setFont('helvetica', 'italic'); doc.setFontSize(8.5); doc.setTextColor(100, 110, 120);
                const lines = doc.splitTextToSize(`Vacaciones: ${rangosTexto}`, 265);
                lines.forEach(line => {
                    if (text_y > 185) { doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages()); text_y = 28; }
                    doc.text(line, 20, text_y); text_y += 4.5;
                });
                doc.setFont('helvetica', 'normal'); doc.setFontSize(9.5); doc.setTextColor(44, 62, 80); text_y += 2.0;
                if (text_y > 185) { doc.addPage(); drawHeaderFooter(doc, doc.internal.getNumberOfPages()); text_y = 28; }
            });
        }

        doc.save(`Calendario_Vacaciones_Tabla_${data.year}.pdf`);
    }

    static exportToExcel(data) {
        let csv = "TRABAJADOR,DIAS_BASE,DIAS_EXTRAS\n";
        Object.keys(data.trabajadores).forEach(w => {
            const inf = data.trabajadores[w];
            csv += `"${w}",${inf.dias_base},${inf.dias_extras}\n`;
        });
        this.downloadFile(csv, `export_config_personal_${data.year}.csv`, 'text/csv');
    }

    static exportTrabajadorJson(data, trabajador) {
        if (!data.trabajadores[trabajador]) return;
        const exportObj = {
            [trabajador]: data.trabajadores[trabajador]
        };
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(exportObj, null, 2));
        const a = document.createElement('a'); a.href = dataStr; a.download = `vacaciones_${trabajador}.json`; a.click();
    }
}


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

function importarDataInteligente(currentData, text, isJson) {
    let data = { ...currentData };
    if (isJson) {
        const p = JSON.parse(text);
        if (p.trabajadores || p.festivos || p.titulo_pagina) {
            return { ...data, ...p }; // Consolidado
        } else if (Array.isArray(p)) {
            // Festivos
            const fests = p.filter(f => typeof f === 'string' && /^\d{2}\/\d{2}\/\d{4}$/.test(f));
            data.festivos = [...new Set([...data.festivos, ...fests])];
            return data;
        } else {
            // Config / Trabajadores
            Object.keys(p).forEach(k => {
                if (typeof p[k] === 'object' && ('dias_base' in p[k] || 'dias_extras' in p[k])) {
                    data.trabajadores[k] = { ...(data.trabajadores[k] || { vacaciones: [], dias_base: 22, dias_extras: 0 }), ...p[k] };
                } else if (Array.isArray(p[k])) {
                    const fests = [];
                    p[k].forEach(item => {
                        if (typeof item === 'string' && /^\d{2}\/\d{2}\/\d{4}$/.test(item)) fests.push(item);
                        else if (item && item.fecha && /^\d{2}\/\d{2}\/\d{4}$/.test(item.fecha)) fests.push(item.fecha);
                    });
                    data.trabajadores[k] = { ...(data.trabajadores[k] || { vacaciones: [], dias_base: 22, dias_extras: 0 }), vacaciones: fests };
                }
            });
            return data;
        }
    } else {
        const lines = text.split(/\r?\n/).filter(x => x.trim());
        if (!lines.length) return data;
        const filas = lines.map(parseCSVLine);
        if (filas[0].length === 1 && /^\d{2}\/\d{2}\/\d{4}$/.test(filas[0][0])) {
            const fests = filas.map(r => r[0]);
            data.festivos = [...new Set([...data.festivos, ...fests])];
        } else if (filas[0].length >= 2 && !isNaN(parseInt(filas[0][1]))) {
            filas.forEach(row => {
                const name = row[0]; if (!name) return;
                const db = parseInt(row[1]) || 22; const de = parseInt(row[2]) || 0;
                data.trabajadores[name] = { ...(data.trabajadores[name] || { vacaciones: [] }), dias_base: db, dias_extras: de };
            });
        } else {
            // Vacaciones asignadas CSV
            filas.forEach(row => {
                const name = row[0]; if (!name) return;
                const fests = [];
                for (let i = 1; i < row.length; i++) {
                    let d = row[i];
                    if (d.includes(':')) d = d.split(':')[0]; // remove year qouta
                    if (/^\d{2}\/\d{2}\/\d{4}$/.test(d)) fests.push(d);
                }
                data.trabajadores[name] = { ...(data.trabajadores[name] || { vacaciones: [], dias_base: 22, dias_extras: 0 }), vacaciones: fests };
            });
        }
    }
    return data;
}

const Header = ({ data, onImportJson, onOpenConfig }) => html`
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
                                <li><button className="dropdown-item" onClick=${() => ExportService.exportToPdfMensual(data)}><i className="bi bi-file-pdf text-danger me-2"></i>PDF (Vista Mensual)</button></li>
                                <li><button className="dropdown-item" onClick=${() => ExportService.exportToPdfGantt(data)}><i className="bi bi-file-pdf text-danger me-2"></i>PDF (Vista Gantt)</button></li>
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

const ConfigDialog = ({ show, data, onClose, onSave }) => {
    if (!show) return null;
    const [local, setLocal] = useState({ ...data, meses_a_mostrar: data.meses_a_mostrar || [6, 7, 8, 9] });
    const [newHoliday, setNewHoliday] = useState("");
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    const fetchHolidays = async () => {
        const f = await HolidayService.fetchHolidays(local.comunidadAutonoma, local.year);
        setLocal({ ...local, festivos: f });
        alert(`Festivos importados para ${local.comunidadAutonoma}`);
    };

    const toggleMes = (mesIdx) => {
        let ms = [...local.meses_a_mostrar];
        if (ms.includes(mesIdx)) ms = ms.filter(x => x !== mesIdx);
        else ms.push(mesIdx);
        ms.sort((a, b) => a - b);
        setLocal({ ...local, meses_a_mostrar: ms });
    };

    const addManualHoliday = () => {
        if (!newHoliday) return;
        const [y, m, d] = newHoliday.split("-");
        if (y && m && d) {
            const dateStr = `${d}/${m}/${y}`;
            if (!local.festivos.includes(dateStr)) {
                setLocal({ ...local, festivos: [...local.festivos, dateStr] });
            }
            setNewHoliday("");
        }
    };

    return html`
                <div className="modal d-block" style=${{ background: 'rgba(0,0,0,0.5)', overflowY: 'auto' }}>
                    <div className="modal-dialog modal-lg"><div className="modal-content">
                        <div className="modal-header bg-light">
                            <h5 className="modal-title"><i className="bi bi-sliders me-2"></i>Configuración</h5>
                            <button className="btn-close" onClick=${onClose}></button>
                        </div>
                        <div className="modal-body">
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
                                            <input type="date" className="form-control form-control-sm" value=${newHoliday} onChange=${e => setNewHoliday(e.target.value)} />
                                            <button className="btn btn-sm btn-success" onClick=${addManualHoliday}><i className="bi bi-plus"></i></button>
                                        </div>
                                    </div>
                                    <div className="d-flex justify-content-between align-items-center mt-2">
                                        <div className="text-muted small fw-bold">Festivos cargados actualmente: ${local.festivos?.length || 0}</div>
                                        <button className="btn btn-sm btn-outline-danger" onClick=${() => setLocal({ ...local, festivos: [] })}>Borrar Todos</button>
                                    </div>
                                    <div className="d-flex flex-wrap gap-1 mt-2 p-2 border rounded bg-light" style=${{ maxHeight: '100px', overflowY: 'auto' }}>
                                        ${local.festivos && local.festivos.length > 0 ?
            local.festivos.map(f => html`<span key=${f} className="badge bg-secondary d-flex align-items-center gap-1">${f} <i className="bi bi-x-circle cursor-pointer" onClick=${() => setLocal({ ...local, festivos: local.festivos.filter(x => x !== f) })}></i></span>`)
            : html`<span className="text-muted small">No hay festivos cargados</span>`
        }
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
                                        ${nombresMeses.map((nm, idx) => html`
                                            <div key=${idx} className="form-check form-check-inline" style=${{ width: '100px' }}>
                                                <input className="form-check-input" type="checkbox" id=${"m" + idx} checked=${local.meses_a_mostrar.includes(idx + 1)} onChange=${() => toggleMes(idx + 1)} />
                                                <label className="form-check-label" htmlFor=${"m" + idx}>${nm}</label>
                                            </div>
                                        `)}
                                    </div>
                                </div>
                            </div>
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

const CalendarGrid = ({ data, activeWorker, onToggleDay }) => {
    const meses = (data.meses_a_mostrar || [6, 7, 8, 9]).map(m => m - 1);
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];
    const daysHeader = ["L", "M", "X", "J", "V", "S", "D"];

    const [isDragging, setIsDragging] = useState(false);
    const [dragMode, setDragMode] = useState(null);

    const handleMouseDown = (dateStr, isActiveWorkerOnVac) => {
        setIsDragging(true);
        const mode = isActiveWorkerOnVac ? 'remove' : 'add';
        setDragMode(mode);
        onToggleDay(dateStr, mode);
    };

    const handleMouseEnter = (dateStr) => {
        if (isDragging && dragMode) {
            onToggleDay(dateStr, dragMode);
        }
    };

    const handleMouseUp = () => {
        setIsDragging(false);
        setDragMode(null);
    };

    useEffect(() => {
        window.addEventListener('mouseup', handleMouseUp);
        return () => window.removeEventListener('mouseup', handleMouseUp);
    }, []);

    return html`
                <div className="row g-4 mt-2" style=${{ userSelect: 'none' }}>
                    ${meses.map(m => {
        const startOffset = (new Date(data.year, m, 1).getDay() || 7) - 1;
        const totalDias = new Date(data.year, m + 1, 0).getDate();
        const emptyCells = Array.from({ length: startOffset }, (_, i) => i);
        const days = Array.from({ length: totalDias }, (_, i) => i + 1);

        return html`
                            <div key=${m} className="col-12 col-xl-6">
                                <div className="card shadow-sm h-100 border-0">
                                    <div className="card-header bg-white border-bottom-0 pt-3">
                                        <h5 className="mb-0 text-center fw-bold text-secondary">${nombresMeses[m]} ${data.year}</h5>
                                    </div>
                                    <div className="card-body p-3">
                                        <div className="month-grid fw-bold text-muted mb-2">
                                            ${daysHeader.map(d => html`<div key=${d}>${d}</div>`)}
                                        </div>
                                        <div className="month-grid">
                                            ${emptyCells.map(i => html`<div key=${"empty-" + i} className="p-2"></div>`)}
                                            ${days.map(d => {
            const dateStr = `${String(d).padStart(2, '0')}/${String(m + 1).padStart(2, '0')}/${data.year}`;
            const dayOfWeek = new Date(data.year, m, d).getDay();
            const isWeekend = dayOfWeek === 0 || dayOfWeek === 6;
            const isHoliday = data.festivos.includes(dateStr);

            let bgClass = "bg-white border";
            let textClass = "text-dark";

            if (isHoliday) { bgClass = "bg-danger opacity-75 border-danger"; textClass = "text-white"; }
            else if (isWeekend) { bgClass = "bg-light border-light"; textClass = "text-muted"; }

            const workersOnVac = Object.keys(data.trabajadores).filter(w => data.trabajadores[w].vacaciones.includes(dateStr));
            const isActiveWorkerOnVac = activeWorker && workersOnVac.includes(activeWorker);

            if (isActiveWorkerOnVac) {
                bgClass = "bg-primary border-primary";
                textClass = "text-white fw-bold";
            } else if (workersOnVac.length > 0 && !isHoliday && !isWeekend) {
                bgClass = "bg-info bg-opacity-25 border-info";
            }

            return html`
                                                    <div key=${d} 
                                                         className=${`p-1 rounded cursor-pointer day-cell ${bgClass} ${textClass}`}
                                                         style=${{ minHeight: '55px', display: 'flex', flexDirection: 'column' }} 
                                                         onMouseDown=${() => handleMouseDown(dateStr, isActiveWorkerOnVac)}
                                                         onMouseEnter=${() => handleMouseEnter(dateStr)}
                                                         title=${workersOnVac.join('\n')}>
                                                        <div className="text-end pe-1" style=${{ fontSize: '0.9rem' }}>${d}</div>
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

const GanttGrid = ({ data, activeWorker, onToggleDay }) => {
    const meses = (data.meses_a_mostrar || [6, 7, 8, 9]).map(m => m - 1);
    const nombresMeses = ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

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

    return html`
                <div className="mt-3" style=${{ userSelect: 'none' }}>
                    ${meses.map(m => {
        const totalDias = new Date(data.year, m + 1, 0).getDate();
        const days = Array.from({ length: totalDias }, (_, i) => i + 1);
        return html`
                            <div key=${m} className="card shadow-sm mb-4 border-0">
                                <div className="card-header bg-white fw-bold">
                                    ${nombresMeses[m].toUpperCase()} ${data.year}
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
                                            ${Object.keys(data.trabajadores).sort().map(w => {
            const listVacaciones = data.trabajadores[w].vacaciones;
            return html`
                                                    <tr key=${w} className=${w === activeWorker ? "table-primary border-primary" : ""}>
                                                        <td className="text-start fw-bold">${w}</td>
                                                        ${days.map(d => {
                const dateStr = `${String(d).padStart(2, '0')}/${String(m + 1).padStart(2, '0')}/${data.year}`;
                const dayOfWeek = new Date(data.year, m, d).getDay();
                const isWeekend = dayOfWeek === 0 || dayOfWeek === 6;
                const isHoliday = data.festivos.includes(dateStr);
                const isVacacion = listVacaciones.includes(dateStr);

                let bg = ""; let icon = "";
                if (isVacacion) { bg = "bg-primary opacity-50 text-white"; icon = "V"; }
                else if (isHoliday) { bg = "bg-danger opacity-25 text-danger fw-bold"; icon = "F"; }
                else if (isWeekend) { bg = "bg-secondary opacity-25 text-muted"; icon = ""; }

                let cursor = (activeWorker === w || !activeWorker) && !isHoliday && !isWeekend ? "cursor-pointer" : "";

                return html`
                                                                <td key=${d} 
                                                                    className=${`${bg} ${cursor}`} 
                                                                    title=${dateStr}
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

const ResumenVacaciones = ({ data }) => {
    return html`
                <div className="card mt-5 shadow-sm border-0">
                    <div className="card-header bg-white fw-bold text-secondary">
                        <i className="bi bi-card-list me-2"></i>Resumen Anual de Vacaciones (Leyenda)
                    </div>
                    <div className="card-body p-0">
                        <ul className="list-group list-group-flush">
                            ${Object.keys(data.trabajadores).sort().map(w => {
        const cons = contarDiasConsumidos(data.trabajadores[w].vacaciones, data.festivos);
        const limit = data.trabajadores[w].dias_base + data.trabajadores[w].dias_extras;
        const txt = agruparVacacionesEnTexto(data.trabajadores[w].vacaciones, data.festivos, data.year);
        const excede = cons > limit ? html`<span className="badge bg-danger ms-2">Cupo superado!</span>` : null;
        return html`
                                    <li key=${w} className="list-group-item">
                                        <div className="fw-bold text-dark">
                                            [${obtenerIniciales(w)}] ${w}: <span className=${cons > limit ? "text-danger" : "text-primary"}>${cons} de ${limit} días consumidos</span>${excede}
                                        </div>
                                        <div className="text-muted small fst-italic mt-1">Vacaciones: ${txt}</div>
                                    </li>
                                `
    })}
                            ${Object.keys(data.trabajadores).length === 0 ? html`<li className="list-group-item text-muted">No hay trabajadores registrados.</li>` : null}
                        </ul>
                    </div>
                </div>
            `;
};

const App = () => {
    const [data, setData] = useState(null);
    const [activeWorker, setActiveWorker] = useState("");
    const [showConfig, setShowConfig] = useState(false);
    const [newWorkerName, setNewWorkerName] = useState("");
    const [activeTab, setActiveTab] = useState("calendario");

    useEffect(() => { setData(StorageService.loadData()); }, []);
    useEffect(() => { if (data) StorageService.saveData(data); }, [data]);

    if (!data) return html`<div className="p-5 text-center">Cargando...</div>`;

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

    const toggleDay = (dateStr, forceMode = null, specificWorker = null) => {
        const workerName = specificWorker || activeWorker;
        if (!workerName) return;
        const worker = data.trabajadores[workerName];
        const vacs = [...worker.vacaciones];
        const idx = vacs.indexOf(dateStr);

        const day = new Date(dateStr.split("/").reverse().join("-")).getDay();
        if (day === 0 || day === 6 || data.festivos.includes(dateStr)) return;

        if (forceMode === 'add' && idx === -1) {
            vacs.push(dateStr);
        } else if (forceMode === 'remove' && idx > -1) {
            vacs.splice(idx, 1);
        } else if (!forceMode) {
            if (idx > -1) vacs.splice(idx, 1);
            else vacs.push(dateStr);
        }

        setData(prev => ({ ...prev, trabajadores: { ...prev.trabajadores, [workerName]: { ...prev.trabajadores[workerName], vacaciones: vacs } } }));
    };

    const addWorker = () => {
        const name = newWorkerName.trim();
        if (!name) return;
        if (data.trabajadores[name]) return alert("Ya existe un trabajador con ese nombre.");
        setData({ ...data, trabajadores: { ...data.trabajadores, [name]: { vacaciones: [], dias_base: 22, dias_extras: 0, departamento: "General", imputaciones: {} } } });
        setActiveWorker(name);
        setNewWorkerName("");
    };

    const delWorker = () => {
        if (!activeWorker) return;
        if (!confirm(`¿Estás seguro de eliminar a ${activeWorker}?`)) return;
        const t = { ...data.trabajadores };
        delete t[activeWorker];
        setData({ ...data, trabajadores: t });
        setActiveWorker("");
    };

    const exportTrabajadorJson = () => {
        if (!activeWorker) return alert("Selecciona un trabajador");
        ExportService.exportTrabajadorJson(data, activeWorker);
    };

    const cons = activeWorker ? contarDiasConsumidos(data.trabajadores[activeWorker].vacaciones, data.festivos) : 0;
    const lim = activeWorker ? (data.trabajadores[activeWorker].dias_base + data.trabajadores[activeWorker].dias_extras) : 0;

    return html`
                <div style=${{ paddingBottom: '100px' }}>
                    <${Header} 
                        data=${data}
                        onImportJson=${handleImport}
                        onOpenConfig=${() => setShowConfig(true)} 
                    />
                    
                    <div className="container-fluid px-4">
                        <div className="card shadow-sm mb-4 border-0">
                            <div className="card-body bg-white rounded">
                                <div className="row align-items-center gy-3">
                                    <div className="col-12 col-md-auto d-flex gap-2 align-items-center">
                                        <select className="form-select fw-bold text-primary" style=${{ minWidth: '250px' }} value=${activeWorker} onChange=${e => setActiveWorker(e.target.value)}>
                                            <option value="">(Todos los trabajadores)</option>
                                            ${Object.keys(data.trabajadores).sort().map(w => html`<option key=${w} value=${w}>${w}</option>`)}
                                        </select>
                                        ${activeWorker ? html`
                                            <button className="btn btn-outline-danger" title="Eliminar trabajador" onClick=${delWorker}><i className="bi bi-trash"></i></button>
                                            <button className="btn btn-outline-secondary" title="Exportar JSON Individual" onClick=${exportTrabajadorJson}><i className="bi bi-download"></i></button>
                                        ` : null}
                                    </div>
                                    <div className="col-12 col-md-auto d-flex gap-2">
                                        <input className="form-control" placeholder="Nuevo trabajador..." value=${newWorkerName} onChange=${e => setNewWorkerName(e.target.value)} onKeyDown=${e => e.key === 'Enter' && addWorker()} />
                                        <button className="btn btn-success" onClick=${addWorker}><i className="bi bi-person-plus"></i></button>
                                    </div>
                                    
                                    ${activeWorker ? html`
                                        <div className="col-12 col-md-auto ms-md-auto d-flex align-items-center gap-4 bg-light p-2 rounded border">
                                            <div className="text-center">
                                                <div className="text-muted small fw-bold text-uppercase">Días Consumidos</div>
                                                <div className=${`fs-5 fw-bold ${cons > lim ? "text-danger" : "text-primary"}`}>${cons} / ${lim}</div>
                                            </div>
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
                        </ul>

                        ${activeTab === "calendario"
            ? html`<${CalendarGrid} data=${data} activeWorker=${activeWorker} onToggleDay=${toggleDay} />`
            : html`<${GanttGrid} data=${data} activeWorker=${activeWorker} onToggleDay=${toggleDay} />`
        }

                        <${ResumenVacaciones} data=${data} />
                    </div>

                    <${ConfigDialog} show=${showConfig} data=${data} onClose=${() => setShowConfig(false)} onSave=${d => { setData(d); setShowConfig(false); }} />
                </div>
            `;
};

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(html`<${App} />`);
