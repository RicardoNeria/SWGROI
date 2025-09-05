document.addEventListener("DOMContentLoaded", function () {
    function getCookie(nombre) {
        const valor = `; ${document.cookie}`;
        const partes = valor.split(`; ${nombre}=`);
        if (partes.length === 2) return partes.pop().split(';').shift();
        return "";
    }

    const usuario = getCookie("usuario");

    // Validación principal: si no hay cookie, redirigir
    if (!usuario) {
        const msg = new URL(window.location.href).searchParams.get("msg");
        if (msg === "sesion-obligatoria") {
            mostrarAlerta("Es necesario cerrar sesión para salir del sistema.");
        } else {
            window.location.href = "/login.html?msg=sesion-obligatoria";
        }
    }

    window.addEventListener("pageshow", function (event) {
        const usuario = getCookie("usuario");

        if (!usuario) {
            window.location.href = "/login.html?msg=sesion-obligatoria";
        }

    });

    function mostrarAlerta(mensaje) {
        const alerta = document.createElement("div");
        alerta.textContent = mensaje;
        alerta.style.backgroundColor = "#dc3545";
        alerta.style.color = "#fff";
        alerta.style.padding = "12px";
        alerta.style.textAlign = "center";
        alerta.style.fontWeight = "bold";
        alerta.style.position = "fixed";
        alerta.style.top = "0";
        alerta.style.left = "0";
        alerta.style.right = "0";
        alerta.style.zIndex = "9999";
        document.body.prepend(alerta);
    }
});
