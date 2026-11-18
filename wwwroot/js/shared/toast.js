/**
 * toast.js — centralised Bootstrap toast notifications.
 * Single Responsibility: show success/error feedback only.
 */
const Toast = (() => {
    function _show(toastId, messageId, text) {
        const el = document.getElementById(toastId);
        const msg = document.getElementById(messageId);
        if (!el || !msg) return;
        msg.textContent = text;
        new bootstrap.Toast(el).show();
    }

    return {
        success: (text) => _show('successToast', 'successMessage', text),
        error:   (text) => _show('errorToast',   'errorMessage',   text),
    };
})();
