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

    async function start() {
        try {
            await connection.start();
            setStatus('Bağlı', 'bg-success');
            await connection.invoke('JoinRegion', 'default-region');
        } catch (err) {
            console.error('SignalR bağlantı hatası:', err);
            setStatus('Bağlanamadı', 'bg-danger');
        }
    }

    start();
})();