/**
 * dashboard.js — Admin dashboard SignalR + execution management.
 * Depends on: shared/toast.js
 */
(() => {
    // ── Status badge config (Open/Closed — add new statuses here only) ───────
    const STATUS_CONFIG = {
        Succeeded: { badge: 'bg-success',          icon: 'fa-check',           label: 'Completed', row: 'table-success',   autoRemove: true  },
        Completed: { badge: 'bg-success',          icon: 'fa-check',           label: 'Completed', row: 'table-success',   autoRemove: true  },
        Failed:    { badge: 'bg-danger',           icon: 'fa-times',           label: 'Failed',    row: 'table-danger',    autoRemove: true  },
        Cancelled: { badge: 'bg-secondary',        icon: 'fa-ban',             label: 'Cancelled', row: 'table-secondary', autoRemove: true  },
        Running:   { badge: 'bg-warning text-dark',icon: 'fa-spinner fa-spin', label: 'Running',   row: 'table-warning',   autoRemove: false },
        Progress:  { badge: 'bg-info text-dark',   icon: 'fa-spinner fa-spin', label: 'Progress',  row: 'table-info',      autoRemove: false },
    };

    function _statusBadge(status, progress) {
        const cfg = STATUS_CONFIG[status];
        if (!cfg) return `<span class="badge bg-secondary">${status}</span>`;
        const label = (status === 'Progress' && progress != null) ? `Progress #${progress}` : cfg.label;
        return `<span class="badge ${cfg.badge}"><i class="fas ${cfg.icon} me-1"></i>${label}</span>`;
    }

    // ── SignalR ──────────────────────────────────────────────────────────────
    if (typeof signalR === 'undefined') return;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/executionHub')
        .withAutomaticReconnect()
        .build();
connection.start().catch(() => {});
    connection.on('ExecutionStarted',   (id, name)         => _addRow(id, name, 'Running'));
    connection.on('ExecutionCompleted', (id, status)       => _updateRow(id, status));
    connection.on('ExecutionCancelled', (id)               => _updateRow(id, 'Cancelled'));
    connection.on('ExecutionFailed',    (id)               => _updateRow(id, 'Failed'));
    connection.on('ExecutionProgress',  (id, progress)     => _updateRow(id, 'Progress', progress));

    // ── Row management ───────────────────────────────────────────────────────
    function _addRow(executionId, reportName, status) {
         if (document.getElementById(`exec-${executionId}`)) return; // already exists (duplicate event)
        const row = document.createElement('tr');
        row.id = `exec-${executionId}`;
        row.innerHTML = `
            <td>${executionId}</td>
            <td>Admin</td>
            <td>${reportName}</td>
            <td class="status">${_statusBadge(status)}</td>
            <td>Just now</td>
            <td>—</td>
            <td>
                <button class="btn btn-sm btn-danger btn-cancel" data-id="${executionId}">
                    <i class="fas fa-times"></i> Cancel
                </button>
            </td>`;
        document.getElementById('executions-table-body')?.prepend(row);
    }

    function _updateRow(executionId, status, progress) {
        const row = document.getElementById(`exec-${executionId}`);
        if (!row) return;

        const cfg = STATUS_CONFIG[status];
        if (!cfg) return;
        row.querySelector('.status').innerHTML = _statusBadge(status, progress);
        row.className = cfg.row;
        if (cfg.autoRemove) setTimeout(() => row.remove(), 2000);
    }

    // ── Cancel execution ─────────────────────────────────────────────────────
    const _cancelModal  = new bootstrap.Modal(document.getElementById('dashboardCancelModal'));
    const _confirmBtn   = document.getElementById('dashboardCancelConfirmBtn');
    let   _pendingCancelId = null;

    document.addEventListener('click', (e) => {
        const btn = e.target.closest('.btn-cancel');
        if (!btn) return;
        _pendingCancelId = btn.dataset.id;
        _cancelModal.show();
    });

    _confirmBtn?.addEventListener('click', () => {
        if (!_pendingCancelId) return;
        _cancelModal.hide();

        $.ajax({
            url:    '/Home/CancelExecution',
            method: 'POST',
            data:   { executionId: _pendingCancelId },
            success: () => Toast.success('Cancellation requested.'),
            error:   () => Toast.error('Failed to cancel execution.'),
            complete: () => { _pendingCancelId = null; }
        });
    });
})();
