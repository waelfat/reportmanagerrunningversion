/**
 * executereportpartial.js — SignalR listener + report execution UI.
 * Depends on: shared/toast.js
 */

// ── SignalR (shared connection, initialised once) ────────────────────────────

if (typeof signalR !== 'undefined') {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/executionHub')
        .withAutomaticReconnect()
        .build();

    connection.start().catch(() => {});

    connection.on('ExecutionCompleted', (executionId, status, duration, hasResult) => {
        updateExecutionRow(executionId, status, duration, hasResult);
        if (status !== 'In Progress') showNotification(status, executionId);
    });

    connection.on('ExecutionStarted', (executionId) => {
        updateExecutionRow(executionId, 'Running', 0, false);
    });

    connection.on('ExecutionProgress', (executionId, progress) => {
        updateExecutionRow(executionId, 'Progress', 0, false, progress);
    });
}

// ── Per-report initialisation (called after partial is injected into DOM) ────
function IntializePartial(reportId) {
    const form    = $(`#executeReportForm-${reportId}`);
    const $btn    = $(`#executeBtn-${reportId}`);
    const $status = $(`#executeStatus-${reportId}`);
    const $result = $(`#executeResult-${reportId}`);

    // ── Execute form submit ──────────────────────────────────────────────────
    form.on('submit', function (e) {
        e.preventDefault();

        let isValid = true;
        form.find('[data-IsRequired="true"]').each(function () {
            const empty = !$(this).val();
            $(this).toggleClass('is-invalid', empty);
            if (empty) isValid = false;
        });

        if (!isValid) {
            $status.html('<div class="text-danger"><i class="fas fa-exclamation-circle me-1"></i>Please fill all required parameters</div>');
            return;
        }

        $status.html('<div class="d-flex align-items-center text-primary"><div class="spinner-border spinner-border-sm me-2" role="status"></div>Executing...</div>');
        $btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin me-2"></i>Executing...');

        $.ajax({
            url:    form.attr('action'),
            method: 'POST',
            data:   form.serialize(),
            success(resp) {
                $status.html('<div class="text-success"><i class="fas fa-check-circle me-1"></i>Started Successfully</div>');
                $result.show().find('.alert')
                    .attr('class', 'alert alert-success')
                    .html(`<i class="fas fa-info-circle me-2"></i><strong>Execution Started!</strong><br>Execution ID: ${resp.executionId || 'n/a'}`);
                    addExecutionToTable(resp.executionId, resp.scheduled, resp.scheduledTime, resp.parameters);
                
               
                var execution={
                     ExecutionID: resp.executionId,
            UserName: resp.userName || '',
            ReportJob: resp.reportJob || '',
            Status: resp.status || 'Pending',
            Started: resp.started || new Date().toISOString(),
            Duration: resp.duration || '00:00:00'
                }
                ExecutionManager.save(execution)

            },
            error(xhr) {
                const msg = xhr.responseJSON?.error || xhr.responseText || 'Failed to start execution';
                $status.html('<div class="text-danger"><i class="fas fa-exclamation-circle me-1"></i>Failed</div>');
                $result.show().find('.alert')
                    .attr('class', 'alert alert-danger')
                    .html(`<i class="fas fa-exclamation-triangle me-2"></i><strong>Execution Failed!</strong><br>${msg}`);
            },
            complete() {
                setTimeout(() => {
                    $btn.prop('disabled', false).html('<i class="fas fa-play me-2"></i>Execute Report');
                    
                }, 1000);
            }
        });
    });

    // ── Cancel confirmation modal ────────────────────────────────────────────
    let confirmCancelId = null;

    document.getElementById('confirmModal')?.addEventListener('show.bs.modal', (event) => {
        confirmCancelId = event.relatedTarget?.getAttribute('data-execution-id');
    });

    $(document).off('click', '#btnConfirmCancel').on('click', '#btnConfirmCancel', () => {
        if (!confirmCancelId) return;
        $.ajax({
            url:    '/Home/CancelExecution',
            method: 'POST',
            data:   { executionId: confirmCancelId },
            success() {
                showNotification('Cancelled', confirmCancelId);
                updateExecutionRow(confirmCancelId, 'Cancelled', 0, false);
                // if exists delete it from localStorage
                
                //ExecutionManager.delete(confirmCancelId);
            },
            error() {
                showNotification('Failed', confirmCancelId);
            }
        });
    });
}

// ── Shared table helpers ─────────────────────────────────────────────────────
function addExecutionToTable(executionId, scheduled,ScheduledTime,parameters) {
    const $tbody = $('.executions-table tbody');
    $tbody.find('tr:has(td[colspan="4"])').remove();

    const now     = new Date();
    dateStr = now.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' });
  //  debugger;
    if (scheduled && ScheduledTime) {
dateStr = new Date(ScheduledTime).toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: 'numeric' });

    }
    const timeStr = scheduled && ScheduledTime
        ? new Date(ScheduledTime).toLocaleTimeString('en-US', { hour12: false })
        : now.toLocaleTimeString('en-US', { hour12: false });
     //const timeStr =  now.toLocaleTimeString('en-US', { hour12: false });
 console.log(ScheduledTime);

 
    const badge  = scheduled
        ? '<span class="badge bg-warning text-dark"><i class="fas fa-calendar-alt me-1"></i>Scheduled</span>'
        : '<span class="badge bg-warning text-dark"><i class="fas fa-spinner fa-spin me-1"></i>Running</span>';
        // if schedule then button will be Delete else Cancel
        const actions = scheduled
        ? `<button class="btn btn-sm btn-outline-danger delete-execution" data-execution-id="${executionId}">
                <i class="fas fa-trash me-1"></i>Delete
            </button>`
        : `<button type="button" class="btn btn-danger btn-sm"
                data-bs-toggle="modal" data-bs-target="#confirmModal"
                data-execution-id="${executionId}">
            <i class="fas fa-stop me-1"></i> Cancel
        </button>`;
    //

    // const actions = `
    //     <button type="button" class="btn btn-danger btn-sm"
    //             data-bs-toggle="modal" data-bs-target="#confirmModal"
    //             data-execution-id="${executionId}">
    //         <i class="fas fa-stop me-1"></i> Cancel
    //     </button>`;
var parametersTooltip = '';
if (parameters && parameters.length > 0) {
    parametersTooltip = parameters.map(p => `${p.name}: ${p.value}`).join('\n');
}
    $tbody.prepend(`
        <tr class="table-warning" data-execution-id="${executionId}" title="${parametersTooltip}" data-bs-toggle="tooltip" data-bs-placement="top">
            <td class="text-nowrap">
                <div class="fw-semibold">${dateStr}</div>
                <small class="text-muted">${timeStr}</small>
            </td>
            <td>${badge}</td>
            <td><span class="text-muted">—</span></td>
            <td class="text-end"><div class="btn-group" role="group">${actions}</div></td>
        </tr>`);
}

function updateExecutionRow(executionId, status, duration, hasResult, progressNum) {
    const $row = $(`.executions-table tbody tr[data-execution-id="${executionId}"]`);
    if ($row.length === 0) return;

    const STATUS_MAP = {
        Succeeded: { badge: 'bg-success',           icon: 'fa-check',            label: 'Succeeded',          row: 'table-success'   },
        Failed:    { badge: 'bg-danger',            icon: 'fa-times',            label: 'Failed',             row: 'table-danger'    },
        Cancelled: { badge: 'bg-secondary',         icon: 'fa-ban',              label: 'Cancelled',          row: 'table-secondary' },
        Running:   { badge: 'bg-warning text-dark', icon: 'fa-spinner fa-spin',  label: 'Running',            row: 'table-warning'   },
        Progress:  { badge: 'bg-info text-dark',    icon: 'fa-spinner fa-spin',  label: `Progress #${progressNum}`, row: 'table-info' },
    };

    const cfg = STATUS_MAP[status];
    if (!cfg) return;

    $row.removeClass('table-warning table-success table-danger table-secondary table-info').addClass(cfg.row);
    $row.find('td:eq(1)').html(`<span class="badge ${cfg.badge}"><i class="fas ${cfg.icon} me-1"></i>${cfg.label}</span>`);

    if (isNumber(duration) && duration >= 0)
        $row.find('td:eq(2)').html(`<span class="badge bg-light text-dark"><i class="fas fa-clock me-1"></i>${formatSeconds(duration)}</span>`);

    if (status === 'Progress') return;

    const deleteBtn = `<button class="btn btn-sm btn-outline-danger delete-execution" data-execution-id="${executionId}"><i class="fas fa-trash me-1"></i>Delete</button>`;

    if (hasResult) {
        $row.find('td:eq(3)').html(`
            <div class="btn-group" role="group">
                <a class="btn btn-sm btn-outline-primary" href="/Home/DownloadExecutionResult/${executionId}">
                    <i class="fas fa-download me-1"></i>Download
                </a>
                ${deleteBtn}
            </div>`);
    } else if (status === 'Cancelled' || status === 'Failed') {
        $row.find('td:eq(3)').html(`<div class="btn-group" role="group">${deleteBtn}</div>`);
    } else {
        $row.find('td:eq(3)').html(`
            <div class="btn-group" role="group">
                <button type="button" class="btn btn-danger btn-sm"
                        data-bs-toggle="modal" data-bs-target="#confirmModal"
                        data-execution-id="${executionId}">
                    <i class="fas fa-stop me-1"></i>Cancel
                </button>
            </div>`);
    }
}

function showNotification(status, executionId) {
    const CONFIG = {
        Succeeded: { cls: 'alert-success', icon: 'fa-check-circle',       msg: 'Report execution completed successfully!' },
        Cancelled: { cls: 'alert-warning', icon: 'fa-ban',                msg: 'Report execution was cancelled!'          },
        Failed:    { cls: 'alert-danger',  icon: 'fa-exclamation-circle', msg: 'Report execution failed!'                 },
    };
    const cfg = CONFIG[status] || CONFIG.Failed;

    const el = document.createElement('div');
    el.className = `alert ${cfg.cls} alert-dismissible fade show position-fixed`;
    el.style.cssText = 'top:20px;right:20px;z-index:9999;min-width:300px;';
    el.setAttribute('role', 'alert');
    el.innerHTML = `<i class="fas ${cfg.icon} me-2"></i>${cfg.msg}
        <button type="button" class="btn-close" onclick="this.parentElement.remove()"></button>`;

    document.body.appendChild(el);
    setTimeout(() => el.remove(), 5000);
}
function formatSeconds(totalSeconds) {
  // Math.floor(totalSeconds / 3600) keeps adding days as 24+ hours
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = Math.floor(totalSeconds % 60);

  const pad = (num) => String(num).padStart(2, '0');

  return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}
function isNumber(value) {
  return typeof value === 'number' && Number.isFinite(value);
}
