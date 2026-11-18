/**
 * CreateReport.js — wiring for the Create Report page only.
 * Depends on: shared/toast.js, shared/api.js, Report/parameterManager.js
 */
(() => {
    let extractedParameters = [];
    let reportMode = 'SQLSTATEMENT';

    // ── Report type toggle ───────────────────────────────────────────────────
    document.querySelectorAll('input[name="ReportTypeRadio"]').forEach(radio => {
        radio.addEventListener('change', function () {
            reportMode = this.value;
            const isSP = reportMode === 'STOREDPROCEDURE';
            document.getElementById('sqlSection').style.display  = isSP ? 'none'  : 'block';
            document.getElementById('procSection').style.display = isSP ? 'block' : 'none';
            extractedParameters = [];
            document.getElementById('parametersList').innerHTML = '';
            document.getElementById('parametersSection').style.display = 'none';
        });
    });

    // ── Load from SQL query ──────────────────────────────────────────────────
    document.getElementById('loadParametersBtn').addEventListener('click', () => {
        const query = document.getElementById('ReportQuery').value.trim();
        if (!query) { Toast.error('Please enter a SQL query first'); return; }

        const existing = ParameterManager.collect();
        extractedParameters = ParameterManager.extractFromQuery(query);

        if (extractedParameters.length === 0) {
            Toast.error('No parameters found. Use :parameterName syntax.');
            return;
        }

        extractedParameters.forEach((p, i) => {
            if (existing[p.name]) p.existing = existing[p.name];
            p.position = i + 1;
        });

        _render();
        Toast.success(`Found ${extractedParameters.length} parameter(s)`);
    });

    // ── Load from stored procedure ───────────────────────────────────────────
    document.getElementById('loadProcParamsBtn').addEventListener('click', async () => {
        const procName = document.getElementById('ProcedureName').value.trim();
        const schemaId = document.getElementById('SchemaId').value;
        if (!procName) { Toast.error('Please enter a procedure name'); return; }
        if (!schemaId) { Toast.error('Please select a schema first'); return; }

        try {
            const params = await Api.getProcedureParameters(procName, schemaId);
            if (params.length === 0) { Toast.error('No parameters found for this procedure'); return; }

            extractedParameters = params.map((p, i) => ({
                name: p.name, position: p.position || i + 1, type: p.type, direction: p.direction
            }));

            _render();
            Toast.success(`Loaded ${extractedParameters.length} parameter(s) from database`);
        } catch (err) {
            Toast.error('Error: ' + err.message);
        }
    });

    // ── Add manual parameter ─────────────────────────────────────────────────
    document.getElementById('addManualParamBtn').addEventListener('click', () => {
        const name = document.getElementById('newParamNameInput')?.value.trim();
        if (!name) { Toast.error('Please enter a parameter name'); return; }
        extractedParameters.push({ name, position: extractedParameters.length + 1, direction: 'Input', type: 'Varchar2' });
        document.getElementById('newParamNameInput').value = '';
        _render();
    });

    // ── Submit ───────────────────────────────────────────────────────────────
    document.getElementById('createReportForm').addEventListener('submit', async (e) => {
        e.preventDefault();

        const isSP     = reportMode === 'STOREDPROCEDURE';
        const procName = isSP ? document.getElementById('ProcedureName').value.trim() : null;
        if (isSP && !procName) { Toast.error('Please enter a procedure name'); return; }
        if (!isSP && extractedParameters.length === 0) { Toast.error('Please load parameters first'); return; }

        const formData = {
            Name:        document.getElementById('Name').value,
            Description: document.getElementById('Description').value,
            ReportQuery: isSP ? procName : document.getElementById('ReportQuery').value,
            SchemaId:    document.getElementById('SchemaId').value,
            IsActive:    document.getElementById('IsActive').checked,
            CategoryId:  document.getElementById('CategoryId').value,
            ReportType:  reportMode,
            Parameters:  ParameterManager.collectAsArray(),
        };

        try {
            await Api.createReport(formData);
            Toast.success('Report created successfully');
            setTimeout(() => { window.location.href = '/Home/Index'; }, 1500);
        } catch (err) {
            Toast.error('Error: ' + err.message);
        }
    });

    // ── Internal ─────────────────────────────────────────────────────────────
    function _render() {
        const isSP = reportMode === 'STOREDPROCEDURE';
        ParameterManager.display(extractedParameters, isSP, (removedIndex) => {
            extractedParameters.splice(removedIndex, 1);
            _render();
        });
        document.getElementById('parametersSection').style.display = 'block';
    }
})();
