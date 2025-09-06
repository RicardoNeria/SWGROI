document.addEventListener("DOMContentLoaded", function () {
    const cerrarSesion = document.getElementById("cerrarSesion");
    if (cerrarSesion) {
        cerrarSesion.addEventListener("click", function (e) {
            e.preventDefault();
            window.location.href = "/logout";
        });
    }

    const url = new URL(window.location.href);
    const msg = url.searchParams.get("msg");
    if (msg === "sesion-activa") {
        const leyenda = document.getElementById("leyenda");
        leyenda.innerText = "Es necesario cerrar sesión para poder regresar al INICIO DE SESIÓN.";
        leyenda.style.color = "red";
        leyenda.style.display = "block";
        setTimeout(() => { leyenda.style.display = "none"; }, 3000);
    }

    // Usa helper global expuesto por seguridad.js para obtener cookies decodificadas
    const rol = (window.getCookie ? window.getCookie("rol") : "");

    console.log("Rol actual:", rol);
});
