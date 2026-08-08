let maps = {};
let polygons = {};
let drawMarkers = {};
let clientMarkers = {};
let zonePolygons = {};

window.leafletHelper = {
    initMap: function (elementId, dotNetRef, defaultLat, defaultLng, zoom) {
        if (maps[elementId]) {
            maps[elementId].remove();
        }

        // Guadalajara por defecto si no hay coordenadas
        let lat = defaultLat || 20.659698;
        let lng = defaultLng || -103.349609;
        let mapZoom = zoom || 13;

        let map = L.map(elementId).setView([lat, lng], mapZoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);

        maps[elementId] = map;
        polygons[elementId] = null;
        drawMarkers[elementId] = [];
        zonePolygons[elementId] = [];

        map.on('click', function (e) {
            dotNetRef.invokeMethodAsync('OnMapClicked', e.latlng.lat, e.latlng.lng);
        });

        // Forzar redibujado de Leaflet para corregir cortes de grilla
        setTimeout(() => {
            map.invalidateSize();
        }, 300);
    },

    updateDrawPolygon: function (elementId, coords) {
        let map = maps[elementId];
        if (!map) return;

        // Limpiar marcador anterior y polígono
        if (polygons[elementId]) {
            map.removeLayer(polygons[elementId]);
        }
        drawMarkers[elementId].forEach(marker => map.removeLayer(marker));
        drawMarkers[elementId] = [];

        if (!coords || coords.length === 0) return;

        // Dibujar marcador por coordenada
        coords.forEach((coord, idx) => {
            let marker = L.marker([coord[0], coord[1]], {
                draggable: false
            }).addTo(map);
            marker.bindPopup(`Punto ${idx + 1}`);
            drawMarkers[elementId].push(marker);
        });

        // Si son 3 o más, dibujar polígono cerrado, si no, una línea
        if (coords.length >= 3) {
            polygons[elementId] = L.polygon(coords, { color: '#5C6BC0', fillColor: '#9FA8DA', fillOpacity: 0.5 }).addTo(map);
        } else if (coords.length > 1) {
            polygons[elementId] = L.polyline(coords, { color: '#5C6BC0' }).addTo(map);
        }
    },

    showClientLocation: function (elementId, lat, lng, clientName) {
        let map = maps[elementId];
        if (!map) return;

        if (clientMarkers[elementId]) {
            map.removeLayer(clientMarkers[elementId]);
        }

        if (lat && lng) {
            let marker = L.marker([lat, lng], {
                icon: L.icon({
                    iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
                    shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/0.7.7/images/marker-shadow.png',
                    iconSize: [25, 41],
                    iconAnchor: [12, 41],
                    popupAnchor: [1, -34],
                    shadowSize: [41, 41]
                })
            }).addTo(map);

            marker.bindPopup(clientName || "Cliente").openPopup();
            clientMarkers[elementId] = marker;
            map.setView([lat, lng], 15);
        }
    },

    showZones: function (elementId, zones) {
        let map = maps[elementId];
        if (!map) return;

        // Limpiar zonas anteriores
        zonePolygons[elementId].forEach(layer => map.removeLayer(layer));
        zonePolygons[elementId] = [];

        if (!zones || zones.length === 0) return;

        zones.forEach(zone => {
            try {
                let coords = JSON.parse(zone.polygonCoordinatesJson);
                if (coords && coords.length >= 3) {
                    let poly = L.polygon(coords, {
                        color: '#4CAF50',
                        fillColor: '#81C784',
                        fillOpacity: 0.3
                    }).addTo(map);
                    poly.bindPopup(`<strong>${zone.name}</strong><br>Costo: $${zone.deliveryCost}`);
                    zonePolygons[elementId].push(poly);
                }
            } catch (e) {
                console.error("Error cargando polígono de zona:", e);
            }
        });
    }
};
