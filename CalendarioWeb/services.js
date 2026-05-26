// services.js

// --- MODELS & DEFAULT CONFIG ---
const DEFAULT_CONFIG = {
    ano: new Date().getFullYear(),
    comunidadAutonoma: 'ES-MD', // Madrid by default
    diasPorDefecto: 22,
    festivos: [] // Cache of holidays
};

// --- STORAGE SERVICE ---
class StorageService {
    static STORAGE_KEY = 'CalendarioVacacionesData';

    static loadData() {
        const data = localStorage.getItem(this.STORAGE_KEY);
        if (data) {
            try {
                return JSON.parse(data);
            } catch (e) {
                console.error("Error parsing LocalStorage data", e);
            }
        }
        return {
            config: { ...DEFAULT_CONFIG },
            trabajadores: []
        };
    }

    static saveData(data) {
        localStorage.setItem(this.STORAGE_KEY, JSON.stringify(data));
    }

    static exportJson(data) {
        const dataStr = "data:text/json;charset=utf-8," + encodeURIComponent(JSON.stringify(data, null, 2));
        const downloadAnchorNode = document.createElement('a');
        downloadAnchorNode.setAttribute("href", dataStr);
        downloadAnchorNode.setAttribute("download", `calendario_vacaciones_${data.config.ano}.json`);
        document.body.appendChild(downloadAnchorNode); // required for firefox
        downloadAnchorNode.click();
        downloadAnchorNode.remove();
    }
}

// --- HOLIDAY SERVICE (OpenHolidays API) ---
class HolidayService {
    static async fetchHolidays(countryIso, subdivisionCode, year) {
        // OpenHolidays API format: https://openholidaysapi.org/PublicHolidays?countryIsoCode=ES&languageIsoCode=ES&validFrom=2024-01-01&validTo=2024-12-31&subdivisionCode=ES-MD
        try {
            const startDate = `${year}-01-01`;
            const endDate = `${year}-12-31`;
            let url = `https://openholidaysapi.org/PublicHolidays?countryIsoCode=${countryIso}&languageIsoCode=ES&validFrom=${startDate}&validTo=${endDate}`;
            if (subdivisionCode) {
                url += `&subdivisionCode=${subdivisionCode}`;
            }

            const response = await fetch(url);
            if (!response.ok) throw new Error("Network response was not ok");
            const data = await response.json();
            
            // Map to our simple format
            return data.map(h => ({
                fecha: h.startDate, // YYYY-MM-DD
                nombre: h.name[0].text
            }));
        } catch (error) {
            console.error("Error fetching holidays:", error);
            return [];
        }
    }
}

// --- VALIDATION SERVICE ---
class ValidationService {
    static isOverlap(startDate1, endDate1, startDate2, endDate2) {
        const s1 = new Date(startDate1);
        const e1 = new Date(endDate1);
        const s2 = new Date(startDate2);
        const e2 = new Date(endDate2);
        return s1 <= e2 && s2 <= e1;
    }

    static validateVacation(trabajador, newVacation, editId = null) {
        // Check overlaps
        for (const vac of trabajador.vacaciones) {
            if (editId && vac.id === editId) continue;
            
            if (this.isOverlap(newVacation.fechaInicio, newVacation.fechaFin, vac.fechaInicio, vac.fechaFin)) {
                return { valid: false, message: "El periodo seleccionado se solapa con unas vacaciones existentes." };
            }
        }
        
        // Count total days
        const startDate = new Date(newVacation.fechaInicio);
        const endDate = new Date(newVacation.fechaFin);
        let count = 0;
        let curDate = new Date(startDate);
        while (curDate <= endDate) {
            // Very simple business day count, ignores holidays for this basic check
            // For a full implementation, we'd cross reference with HolidayService
            const day = curDate.getDay();
            if (day !== 0 && day !== 6) {
                count++;
            }
            curDate.setDate(curDate.getDate() + 1);
        }

        // We could also check total allowed days here.
        
        return { valid: true, requestedDays: count };
    }
}

window.StorageService = StorageService;
window.HolidayService = HolidayService;
window.ValidationService = ValidationService;
