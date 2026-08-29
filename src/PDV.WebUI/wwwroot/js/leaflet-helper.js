let maps = {};
let polygons = {};
let drawMarkers = {};
let clientMarkers = {};
let branchMarkers = {};
let zonePolygons = {};
let pickerMarkers = {};

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
        branchMarkers[elementId] = null;
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

    showBranchLocation: function (elementId, lat, lng, branchName) {
        let map = maps[elementId];
        if (!map) return;

        if (branchMarkers[elementId]) {
            map.removeLayer(branchMarkers[elementId]);
            branchMarkers[elementId] = null;
        }

        if (lat != null && lng != null && !isNaN(lat) && !isNaN(lng)) {
            let branchIcon = L.divIcon({
                className: 'custom-branch-marker',
                html: `<div style="background: linear-gradient(135deg, #1976D2 0%, #0D47A1 100%); color: white; border: 2px solid #ffffff; border-radius: 50%; width: 36px; height: 36px; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 10px rgba(0,0,0,0.35); cursor: pointer;"><svg style="width: 20px; height: 20px; fill: white;" viewBox="0 0 24 24"><path d="M20 4H4v2h16V4zm1 10v-2l-1-5H4l-1 5v2h1v6h10v-6h4v6h2v-6h1zm-9 4H6v-4h6v4z"/></svg></div>`,
                iconSize: [36, 36],
                iconAnchor: [18, 18],
                popupAnchor: [0, -20]
            });

            let marker = L.marker([lat, lng], {
                icon: branchIcon,
                zIndexOffset: 1000
            }).addTo(map);

            marker.bindPopup(`<strong>Sucursal:</strong> ${branchName || 'Sucursal'}`);
            branchMarkers[elementId] = marker;
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
    },

    showZonesAndFocus: function (elementId, zones, branchName, branchLat, branchLng) {
        let map = maps[elementId];
        if (!map) return;

        // 1. Mostrar zonas existentes
        window.leafletHelper.showZones(elementId, zones);

        // 2. Colocar marcador de sucursal
        window.leafletHelper.showBranchLocation(elementId, branchLat, branchLng, branchName);

        // 3. Recolectar puntos para encuadrar vista
        let allPoints = [];

        if (branchLat != null && branchLng != null && !isNaN(branchLat) && !isNaN(branchLng)) {
            allPoints.push([branchLat, branchLng]);
        }

        if (zones && zones.length > 0) {
            zones.forEach(zone => {
                try {
                    let coords = JSON.parse(zone.polygonCoordinatesJson);
                    if (coords && Array.isArray(coords)) {
                        coords.forEach(pt => {
                            if (Array.isArray(pt) && pt.length >= 2 && !isNaN(pt[0]) && !isNaN(pt[1])) {
                                allPoints.push([pt[0], pt[1]]);
                            }
                        });
                    }
                } catch (e) {
                    console.error("Error procesando puntos para enfoque:", e);
                }
            });
        }

        // 4. Enfocar mapa según puntos disponibles
        if (allPoints.length > 1) {
            let bounds = L.latLngBounds(allPoints);
            map.fitBounds(bounds, { padding: [50, 50], maxZoom: 15 });
        } else if (allPoints.length === 1) {
            map.setView(allPoints[0], 15);
        } else {
            // Sin coordenadas ni zonas, mantener vista o centrar por defecto
            map.invalidateSize();
        }

        setTimeout(() => {
            map.invalidateSize();
        }, 200);
    },

    centerOnBranch: function (elementId, branchLat, branchLng, zoom) {
        let map = maps[elementId];
        if (!map) return;

        if (branchLat != null && branchLng != null && !isNaN(branchLat) && !isNaN(branchLng)) {
            map.setView([branchLat, branchLng], zoom || 15);
            if (branchMarkers[elementId]) {
                branchMarkers[elementId].openPopup();
            }
        }
    },

    initLocationPicker: function (elementId, dotNetRef, initialLat, initialLng, zoom, markerTitle) {
        if (maps[elementId]) {
            maps[elementId].remove();
            delete maps[elementId];
        }

        let lat = initialLat || 20.659698;
        let lng = initialLng || -103.349609;
        let mapZoom = zoom || (initialLat && initialLng ? 16 : 13);

        let map = L.map(elementId).setView([lat, lng], mapZoom);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);

        maps[elementId] = map;
        pickerMarkers[elementId] = null;

        let createOrMoveMarker = function (mLat, mLng) {
            if (pickerMarkers[elementId]) {
                pickerMarkers[elementId].setLatLng([mLat, mLng]);
            } else {
                let branchIcon = L.divIcon({
                    className: 'custom-branch-marker',
                    html: `<div style="background: linear-gradient(135deg, #1976D2 0%, #0D47A1 100%); color: white; border: 2px solid #ffffff; border-radius: 50%; width: 36px; height: 36px; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 10px rgba(0,0,0,0.35); cursor: pointer;"><svg style="width: 20px; height: 20px; fill: white;" viewBox="0 0 24 24"><path d="M20 4H4v2h16V4zm1 10v-2l-1-5H4l-1 5v2h1v6h10v-6h4v6h2v-6h1zm-9 4H6v-4h6v4z"/></svg></div>`,
                    iconSize: [36, 36],
                    iconAnchor: [18, 18],
                    popupAnchor: [0, -20]
                });

                let marker = L.marker([mLat, mLng], {
                    draggable: true,
                    icon: branchIcon
                }).addTo(map);

                marker.bindPopup(markerTitle || "Ubicación de Sucursal");

                marker.on('dragend', function (e) {
                    let position = e.target.getLatLng();
                    dotNetRef.invokeMethodAsync('OnLocationSelected', position.lat, position.lng);
                });

                pickerMarkers[elementId] = marker;
            }
        };

        if (initialLat && initialLng) {
            createOrMoveMarker(initialLat, initialLng);
        }

        map.on('click', function (e) {
            createOrMoveMarker(e.latlng.lat, e.latlng.lng);
            dotNetRef.invokeMethodAsync('OnLocationSelected', e.latlng.lat, e.latlng.lng);
        });

        setTimeout(() => {
            map.invalidateSize();
        }, 300);
    },

    setLocationPickerMarker: function (elementId, lat, lng, zoom, markerTitle, dotNetRef) {
        let map = maps[elementId];
        if (!map) return;

        if (lat != null && lng != null && !isNaN(lat) && !isNaN(lng)) {
            let mapZoom = zoom || 16;
            map.setView([lat, lng], mapZoom);

            if (pickerMarkers[elementId]) {
                pickerMarkers[elementId].setLatLng([lat, lng]);
                if (markerTitle) {
                    pickerMarkers[elementId].setPopupContent(markerTitle);
                }
            } else {
                let branchIcon = L.divIcon({
                    className: 'custom-branch-marker',
                    html: `<div style="background: linear-gradient(135deg, #1976D2 0%, #0D47A1 100%); color: white; border: 2px solid #ffffff; border-radius: 50%; width: 36px; height: 36px; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 10px rgba(0,0,0,0.35); cursor: pointer;"><svg style="width: 20px; height: 20px; fill: white;" viewBox="0 0 24 24"><path d="M20 4H4v2h16V4zm1 10v-2l-1-5H4l-1 5v2h1v6h10v-6h4v6h2v-6h1zm-9 4H6v-4h6v4z"/></svg></div>`,
                    iconSize: [36, 36],
                    iconAnchor: [18, 18],
                    popupAnchor: [0, -20]
                });

                let marker = L.marker([lat, lng], {
                    draggable: true,
                    icon: branchIcon
                }).addTo(map);

                marker.bindPopup(markerTitle || "Ubicación de Sucursal");

                if (dotNetRef) {
                    marker.on('dragend', function (e) {
                        let position = e.target.getLatLng();
                        dotNetRef.invokeMethodAsync('OnLocationSelected', position.lat, position.lng);
                    });
                }

                pickerMarkers[elementId] = marker;
            }
        }
    },

    removeLocationPickerMarker: function (elementId) {
        let map = maps[elementId];
        if (!map) return;

        if (pickerMarkers[elementId]) {
            map.removeLayer(pickerMarkers[elementId]);
            pickerMarkers[elementId] = null;
        }
    },

    invalidateMapSize: function (elementId) {
        let map = maps[elementId];
        if (map) {
            setTimeout(() => {
                map.invalidateSize();
            }, 100);
        }
    }
};
