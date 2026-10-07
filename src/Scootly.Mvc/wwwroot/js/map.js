(function () {
    const mapElement = document.getElementById('map');
    window.scootlyApiBase = mapElement.dataset.apiBase;
    window.scootlyHubToken = mapElement.dataset.hubToken;
    window.scootlyRegions = JSON.parse(mapElement.dataset.regions || '["default-region"]');

    const fleetView = mapElement.dataset.fleetView === 'true';
    const vehiclesUrl = mapElement.dataset.vehiclesUrl;
    const map = L.map('map').setView([41.0, 29.0], 13);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap katkıda bulunanları',
        maxZoom: 19
    }).addTo(map);

    const markers = {};

    const statusColors = {
        Available: 'green',
        Reserved: 'orange',
        InRide: 'blue',
        Maintenance: 'gray',
        Lost: 'red'
    };

    function colorIcon(status) {
        const color = statusColors[status] || 'gray';
        return L.divIcon({
            className: '',
            html: `<div style="background:${color};width:14px;height:14px;border-radius:50%;border:2px solid white;"></div>`,
            iconSize: [14, 14]
        });
    }

    function popupText(vehicle) {
        return `%${vehicle.batteryPercentage} batarya — ${vehicle.status}`;
    }

    function removeMarker(vehicleId) {
        const marker = markers[vehicleId];

        if (marker) {
            map.removeLayer(marker);
            delete markers[vehicleId];
        }
    }

    async function loadVehicles() {
        try {
            const response = await fetch(vehiclesUrl, { credentials: 'same-origin' });

            if (!response.ok) {
                console.error('Araçlar yüklenemedi:', response.status);
                return;
            }

            const vehicles = await response.json();
            const seen = new Set();

            vehicles.forEach(vehicle => {
                seen.add(vehicle.id);
                const latLng = [vehicle.latitude, vehicle.longitude];
                const icon = colorIcon(vehicle.status);

                if (markers[vehicle.id]) {
                    markers[vehicle.id].setLatLng(latLng).setIcon(icon).setPopupContent(popupText(vehicle));
                } else {
                    markers[vehicle.id] = L.marker(latLng, { icon }).addTo(map).bindPopup(popupText(vehicle));
                }
            });

            // Listeden çıkan (silinen ya da artık görünmeyen) araçların işaretleri kaldırılır.
            Object.keys(markers).filter(id => !seen.has(id)).forEach(removeMarker);
        } catch (err) {
            console.error('Harita verisi alınırken hata:', err);
        }
    }

    // SignalR'dan gelen anlık durum değişiklikleri: konum bir sonraki yenilemede gelir, burada renk güncellenir.
    // Filo görünümü dışındaki kullanıcılar yalnızca müsait araçları görür; müsaitliği biten araç haritadan kalkar.
    window.scootlyUpdateVehicleStatus = function (vehicleId, status) {
        const marker = markers[vehicleId];

        if (!fleetView && status !== 'Available') {
            removeMarker(vehicleId);
            return;
        }

        if (marker) {
            marker.setIcon(colorIcon(status));
        } else if (!fleetView) {
            loadVehicles();
        }
    };

    loadVehicles();
    setInterval(loadVehicles, 30000);
})();
