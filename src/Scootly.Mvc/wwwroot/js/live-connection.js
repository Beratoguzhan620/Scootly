(function () {
    const statusBadge = document.getElementById('connection-status');

    function setStatus(text, cssClass) {
        if (!statusBadge) return;
        statusBadge.textContent = text;
        statusBadge.className = `badge ${cssClass}`;
    }

    if (!window.scootlyHubToken) {
        setStatus('Kimlik doğrulanamadı', 'bg-danger');
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${window.scootlyApiBase}/hubs/fleet`, {
            accessTokenFactory: () => window.scootlyHubToken
        })
        .withAutomaticReconnect()
        .build();

    connection.on('VehicleStatusChanged', (data) => {
        if (window.scootlyUpdateVehicleStatus) {
            window.scootlyUpdateVehicleStatus(data.vehicleId, data.status);
        }
    });

    connection.onreconnecting(() => setStatus('Yeniden bağlanıyor...', 'bg-warning text-dark'));
    connection.onreconnected(() => setStatus('Bağlı', 'bg-success'));
    connection.onclose(() => setStatus('Bağlantı kesildi', 'bg-danger'));

    // Bildirimler aracın bulunduğu hizmet bölgesinin grubuna gider: harita bilinen tüm bölgeleri dinler.
    // Sayfa açıldıktan sonra eklenen bir bölge, sayfa yenilenince dinlenmeye başlar.
    async function joinRegions() {
        for (const region of window.scootlyRegions || ['default-region']) {
            await connection.invoke('JoinRegion', region);
        }
    }

    connection.onreconnected(() => joinRegions().catch(err => console.error('Bölgelere yeniden katılınamadı:', err)));

    async function start() {
        try {
            await connection.start();
            setStatus('Bağlı', 'bg-success');
            await joinRegions();
        } catch (err) {
            console.error('SignalR bağlantı hatası:', err);
            setStatus('Bağlanamadı', 'bg-danger');
        }
    }

    start();
})();