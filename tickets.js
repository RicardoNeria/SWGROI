document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById("formularioTicket");
    const leyenda = document.getElementById("leyenda");

    const responsableInput = document.getElementById("responsable");
    const folioInput = document.getElementById("folio");
    const descripcionInput = document.getElementById("descripcion");
    const estadoInput = document.getElementById("estado");
    const estadoSeguimientoInput = document.getElementById("estadoSeguimiento");
    const comentarioDiv = document.getElementById("comentarioTecnico");
    const actualizarBtn = document.getElementById("actualizarBtn");

    const buscadorFolio = document.getElementById("buscadorFolio");
    const btnBuscar = document.getElementById("btnBuscar");

    const responsableConstante = getResponsableDesdeCookie();
    if (responsableConstante) responsableInput.value = responsableConstante;

    btnBuscar.addEventListener("click", buscarTicketPorFolio);
    buscadorFolio.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
            e.preventDefault();
            buscarTicketPorFolio();
        }
    });

    function buscarTicketPorFolio() {
        const folio = buscadorFolio.value.trim();
        if (!folio) {
            mostrarMensaje("Por favor ingrese un folio para buscar.", false);
            return;
        }

        fetch(`/seguimiento?folio=${encodeURIComponent(folio)}`)
            .then(res => res.json())
            .then(data => {
                if (data && data.Descripcion && data.Estado) {
                    folioInput.value = folio;
                    descripcionInput.value = data.Descripcion || "";
                    estadoInput.value = data.Estado || "Almacén";
                    estadoSeguimientoInput.value = data.Estado || "";
                    comentarioDiv.textContent = data.Comentario || "(Sin comentarios técnicos)";
                    const cerrado = (data.Estado || "").toLowerCase() === "cerrado";
                    folioInput.readOnly = cerrado;
                    descripcionInput.readOnly = cerrado;
                    estadoInput.disabled = cerrado;
                    actualizarBtn.disabled = cerrado;
                    mostrarMensaje(cerrado ? "Este ticket está cerrado y no puede ser editado." : "Ticket encontrado correctamente.", !cerrado);
                } else {
                    mostrarMensaje("No se encontró el ticket.", false);
                }
            })
            .catch(err => {
                mostrarMensaje("Error al buscar el ticket.", false);
                console.error("Error al buscar ticket:", err);
            });
    }

    form.addEventListener("submit", function (e) {
        e.preventDefault();
        mostrarModalConfirmacion("Registrar Ticket", "¿Deseas registrar este ticket?", registrarTicket);
    });

    function registrarTicket() {
        const folio = folioInput.value.trim();
        const descripcion = descripcionInput.value.trim();
        const estado = estadoInput.value;
        const responsable = responsableInput.value.trim();

        if (!folio || !descripcion || !estado || !responsable) {
            mostrarMensaje("Todos los campos son obligatorios.", false);
            return;
        }

        const datos = { Folio: folio, Descripcion: descripcion, Estado: estado, Responsable: responsable, Comentario: "" };

        fetch("/tickets", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(datos)
        })
            .then(res => res.text())
            .then(msg => {
                mostrarMensaje(msg, msg.toLowerCase().includes("registrado") || msg.toLowerCase().includes("correctamente"));
                form.reset();
                responsableInput.value = responsableConstante;
                estadoSeguimientoInput.value = "";
                comentarioDiv.textContent = "(No hay comentarios técnicos aún)";
            })
            .catch(err => {
                console.error("Error al registrar el ticket:", err);
                mostrarMensaje("No se pudo guardar el ticket.", false);
            });
    }

    actualizarBtn.addEventListener("click", function () {
        mostrarModalConfirmacion("Actualizar Ticket", "¿Deseas actualizar este ticket?", actualizarTicket);
    });

    function actualizarTicket() {
        const datos = {
            folio: folioInput.value.trim(),
            descripcion: descripcionInput.value.trim(),
            estado: estadoInput.value,
            responsable: responsableConstante
        };

        fetch("/tickets/actualizar", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(datos)
        })
            .then(res => res.text())
            .then(msg => {
                mostrarMensaje(msg, msg.toLowerCase().includes("actualizado"));
            })
            .catch(err => {
                mostrarMensaje("Error al actualizar el ticket.", false);
                console.error("Actualización error:", err);
            });
    }

    function mostrarMensaje(mensajeTexto, exito, colorPersonalizado) {
        leyenda.innerText = mensajeTexto;
        leyenda.style.backgroundColor = colorPersonalizado || (exito ? "#28a745" : "#e74c3c");
        leyenda.style.color = "white";
        leyenda.style.padding = "10px 20px";
        leyenda.style.borderRadius = "8px";
        leyenda.style.marginBottom = "20px";
        leyenda.style.display = "block";
        setTimeout(() => leyenda.style.display = "none", 4000);
    }

    function getResponsableDesdeCookie() {
        const cookie = document.cookie.split('; ').find(c => c.startsWith('usuario='));
        return cookie ? decodeURIComponent(cookie.split('=')[1]) : '';
    }

    document.getElementById("limpiarBtn").addEventListener("click", function () {
        form.reset();
        estadoInput.selectedIndex = 0;
        estadoSeguimientoInput.value = "";
        comentarioDiv.value = "(No hay comentarios técnicos aún)";
        buscadorFolio.value = "";
    });

    function mostrarModalConfirmacion(titulo, mensaje, callback) {
        const modal = document.getElementById("modalConfirmacion");
        document.getElementById("modalConfirmacionTitulo").textContent = titulo || "Confirmación";
        document.getElementById("modalConfirmacionMensaje").textContent = mensaje || "¿Estás seguro?";
        const btnConfirmar = document.getElementById("btnConfirmarAccion");
        const nuevoBoton = btnConfirmar.cloneNode(true);
        btnConfirmar.parentNode.replaceChild(nuevoBoton, btnConfirmar);
        nuevoBoton.addEventListener("click", () => {
            modal.style.display = "none";
            if (typeof callback === "function") callback();
        });
        modal.style.display = "flex";
    }

    window.cerrarModalConfirmacion = function () {
        document.getElementById("modalConfirmacion").style.display = "none";
    };
});
