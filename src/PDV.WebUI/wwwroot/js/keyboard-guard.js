/**
 * keyboard-guard.js
 * Bloquea el comportamiento nativo del navegador para las teclas de función (F1-F12)
 * en toda la aplicación, dejando que solo la app las maneje.
 *
 * Teclas bloqueadas del navegador:
 *   F1  → Ayuda del navegador
 *   F3  → Buscar en página
 *   F5  → Recargar página
 *   F6  → Enfocar barra de direcciones
 *   F10 → Menú del navegador
 *   F11 → Pantalla completa (se permite: útil en POS)
 *   F12 → DevTools
 *
 * Todas las F-keys disparan el evento hacia Blazor normalmente,
 * solo se cancela la acción nativa del navegador.
 */
(function () {
    // Teclas cuyo comportamiento nativo del navegador queremos cancelar.
    // F11 está comentado intencionalmente: permite alternar pantalla completa, útil en un POS.
    const BLOCKED_BROWSER_KEYS = new Set([
        'F1',   // Ayuda del navegador
        'F2',   // Renombrar (no hace nada por defecto en browser, pero bloqueamos por si acaso)
        'F3',   // Buscar en la página (Ctrl+F)
        'F4',   // Alt+F4 cierra ventana; F4 solo no tiene acción estándar
        'F5',   // Recargar página  ← MUY IMPORTANTE para un POS
        'F6',   // Foco a la barra de URL
        'F7',   // Caret browsing (Firefox)
        'F8',   // Sin acción nativa estándar, pero bloqueamos
        'F9',   // Sin acción nativa estándar, pero bloqueamos
        'F10',  // Menú de la barra de herramientas
        // 'F11', // Pantalla completa — PERMITIDO para que el operador pueda usarlo
        'F12',  // DevTools
    ]);

    window.addEventListener('keydown', function (e) {
        if (BLOCKED_BROWSER_KEYS.has(e.key)) {
            e.preventDefault();
            // e.stopPropagation() NO se llama: Blazor igual recibe el evento
            // porque Blazor usa su propio listener que ya fue registrado.
        }
    }, true); // `true` = fase de captura, se ejecuta ANTES que cualquier otro handler

    console.log('[keyboard-guard] Teclas de función bloqueadas del navegador. F11 permitida.');
})();
