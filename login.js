document.getElementById("formLogin").addEventListener("submit", async function (e) {
    e.preventDefault();

    const usuario = document.getElementById("usuario").value;
    const contrasena = document.getElementById("contrasena").value;
    const mensaje = document.getElementById("mensajeLogin");

    const respuesta = await fetch("/login", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ Usuario: usuario, Contrasena: contrasena })
    });

    const resultado = await respuesta.json();

    if (resultado.exito) {
        // Guardar el rol en una cookie
        document.cookie = `usuario=${usuario}; path=/`;
        document.cookie = `rol=${resultado.rol}; path=/`;


        mensaje.textContent = "Acceso correcto, ingresando al sistema...";
        mensaje.style.color = "green";
        setTimeout(() => {
            window.location.href = "/menu.html";
        }, 1000);
    } else {
        mensaje.textContent = "Usuario o contraseña incorrectos.";
        mensaje.style.color = "red";
    }
});
