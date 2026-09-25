"use strict";

(function () {
    const statusBadge = document.getElementById("ws-status");
    const toastContainer = document.getElementById("ws-toast-container");

    function setStatus(text, colorClass) {
        if (!statusBadge) return;
        statusBadge.textContent = text;
        statusBadge.className = "badge " + colorClass + " align-self-center me-3";
    }

    function showToast(message) {
        if (!toastContainer) return;
        const toastEl = document.createElement("div");
        toastEl.className = "toast align-items-center text-bg-primary border-0";
        toastEl.setAttribute("role", "alert");
        toastEl.innerHTML =
            '<div class="d-flex">' +
            '<div class="toast-body">' + message + '</div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>' +
            '</div>';
        toastContainer.appendChild(toastEl);
        const toast = new bootstrap.Toast(toastEl, { delay: 6000 });
        toast.show();
        toastEl.addEventListener("hidden.bs.toast", () => toastEl.remove());
    }

    function estadoATexto(estado) {
        if (estado === "Aprobado") return "Aprobado";
        if (estado === "Rechazado") return "Rechazado";
        return "Pendiente";
    }

    function badgeClasePorEstado(estado) {
        if (estado === "Aprobado") return "bg-success";
        if (estado === "Rechazado") return "bg-danger";
        return "bg-warning";
    }

    // Actualiza el DOM (tabla "Mis solicitudes" y/o vista "Detalle") si el elemento existe en la página actual
    function actualizarUI(id, estado, motivoRechazo) {
        // Caso tabla "Mis solicitudes"
        const celdaEstado = document.getElementById("estado-cell-" + id);
        if (celdaEstado) {
            celdaEstado.textContent = estadoATexto(estado);
        }

        // Caso vista "Detalle"
        const badge = document.getElementById("estado-badge-" + id);
        if (badge) {
            badge.textContent = estadoATexto(estado);
            badge.className = "badge " + badgeClasePorEstado(estado);

            const motivoLabel = document.getElementById("motivo-label-" + id);
            const motivoValor = document.getElementById("motivo-valor-" + id);
            if (motivoLabel && motivoValor) {
                if (estado === "Rechazado") {
                    motivoLabel.style.display = "block";
                    motivoValor.style.display = "block";
                    motivoValor.textContent = motivoRechazo || "";
                } else {
                    motivoLabel.style.display = "none";
                    motivoValor.style.display = "none";
                }
            }
        }
    }

    // Consulta al servidor el estado vigente de todas las solicitudes del usuario
    // y refresca cualquier elemento presente en la página actual. Se usa al reconectar.
    async function resincronizar() {
        try {
            const respuesta = await fetch("/Solicitudes/MisEstadosJson", { credentials: "same-origin" });
            if (!respuesta.ok) return;
            const lista = await respuesta.json();
            lista.forEach(item => actualizarUI(item.id, item.estado, item.motivoRechazo));
        } catch (err) {
            console.error("No se pudo resincronizar el estado tras la reconexión:", err);
        }
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/solicitudes")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
        .build();

    connection.on("SolicitudEstadoActualizado", function (data) {
        actualizarUI(data.solicitudId, data.estado, data.motivoRechazo);
        const texto = data.estado === "Aprobado"
            ? `Tu solicitud #${data.solicitudId} fue aprobada.`
            : `Tu solicitud #${data.solicitudId} fue rechazada. Motivo: ${data.motivoRechazo}`;
        showToast(texto);
    });

    connection.onreconnecting(() => setStatus("Reconectando...", "bg-warning"));

    connection.onreconnected(async () => {
        setStatus("Conectado", "bg-success");
        await resincronizar(); // recupera cambios ocurridos durante la desconexión
    });

    connection.onclose(() => setStatus("Desconectado", "bg-danger"));

    async function iniciar() {
        try {
            setStatus("Conectando...", "bg-secondary");
            await connection.start();
            setStatus("Conectado", "bg-success");
        } catch (err) {
            console.error("Error al conectar con el hub de solicitudes:", err);
            setStatus("Desconectado", "bg-danger");
            setTimeout(iniciar, 5000);
        }
    }

    iniciar();
})();