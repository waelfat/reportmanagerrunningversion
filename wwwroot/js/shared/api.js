/**
 * api.js — all server communication in one place.
 * Single Responsibility: HTTP requests only.
 */
const Api = (() => {
    async function _post(url, body) {
        const response = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || response.statusText);
        }
        return response.json();
    }

    return {
        getProcedureParameters: (procedureName, schemaId) =>
            _post('/Report/GetProcedureParameters', { procedureName, schemaId }),

        createReport: (formData) =>
            _post('/Report/Create', formData),

        editReport: (formData) =>
            _post('/Report/Edit', formData),
    };
})();
