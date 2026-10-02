(function () {
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

    async function loadVehicles() {
        try {
            const response = await fetch(`${window.scootlyApiBase}/api/v1/vehicles?pageSize=100`);

            if (!response.ok) {
                console.error('Araçlar yüklenemedi:', response.status);
                return;
            }

            const data = await response.json();

            data.items.forEach(vehicle => {
                const latLng = [vehicle.latitude, vehicle.longitude];
                const icon = colorIcon(vehicle.status);

                if (markers[vehicle.id]) {
                    markers[vehicle.id].setLatLng(latLng).setIcon(icon);
                } else {
                    markers[vehicle.id] = L.marker(latLng, { icon }).addTo(map)
                        .bindPopup(`%${vehicle.batteryPercentage} batarya — ${vehicle.status}`);
                }
            });
        } catch (err) {
            console.error('Harita verisi alınırken hata:', err);
        }
    }

    loadVehicles();
    setInterval(loadVehicles, 30000);
})();