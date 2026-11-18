/**
 * parameterManager.js — parameter card UI logic.
 * Single Responsibility: render, collect and manage report parameter cards.
 * Open/Closed: extend by passing options; don't modify internals.
 */
const ParameterManager = (() => {

    // ── Constants ────────────────────────────────────────────────────────────
    const VIEW_CONTROLS  = ['TextBox', 'Select', 'CheckBox', 'Date'];
    const PARAM_TYPES    = ['Varchar2', 'Decimal', 'Date', 'TimeStamp', 'Clob'];
    const DIRECTIONS     = ['Input', 'Output', 'InputOutput'];

    // ── Private helpers ──────────────────────────────────────────────────────
    function _sel(value, options) {
        return options.map(o =>
            `<option value="${o}" ${o === value ? 'selected' : ''}>${o}</option>`
        ).join('');
    }

    function _escape(str) {
        const d = document.createElement('div');
        d.textContent = str ?? '';
        return d.innerHTML;
    }

    function _buildDirectionControl(index, direction, isSP) {
        if (!isSP) return `<input type="hidden" class="param-direction" data-index="${index}" value="Input">`;
        return `
            <div class="mb-2">
                <label class="form-label fw-semibold">Direction <span class="text-danger">*</span></label>
                <select class="form-control param-direction" data-index="${index}" required>
                    ${_sel(direction, DIRECTIONS)}
                </select>
            </div>`;
    }

    function _buildTypeOptions(index, type, isSP) {
        const types = isSP ? [...PARAM_TYPES, 'RefCursor'] : PARAM_TYPES;
        return `
            <div class="mb-2">
                <label class="form-label fw-semibold">Type <span class="text-danger">*</span></label>
                <select class="form-control param-type" data-index="${index}" required>
                    ${_sel(type, types)}
                </select>
            </div>`;
    }

    function _buildCard(param, index, isSP) {
        const ex          = param.existing || {};
        const direction   = ex.direction   || param.direction   || 'Input';
        const type        = ex.type        || param.type        || 'Varchar2';
        const viewControl = ex.viewControl || 'TextBox';
        const isSelect    = viewControl === 'Select';
        const safeName    = _escape(param.name);

        return `
        <div class="col-md-6" id="param-card-${index}">
            <div class="card border-primary border-opacity-25">
                <div class="card-body">
                    <div class="d-flex justify-content-between align-items-center mb-2">
                        <h6 class="text-primary mb-0"><i class="fas fa-tag me-2"></i>${safeName}</h6>
                        ${isSP ? `<button type="button" class="btn btn-sm btn-outline-danger remove-param-btn" data-index="${index}"><i class="fas fa-times"></i></button>` : ''}
                    </div>
                    <input type="hidden" class="param-id"   data-index="${index}" value="${_escape(ex.id || '')}">
                    <input type="hidden" class="param-name" data-index="${index}" value="${safeName}">
                    <div class="mb-2">
                        <label class="form-label fw-semibold">Description</label>
                        <input type="text" class="form-control param-description" data-index="${index}"
                               value="${_escape(ex.description || '')}" placeholder="Parameter description">
                    </div>
                    ${_buildTypeOptions(index, type, isSP)}
                    ${_buildDirectionControl(index, direction, isSP)}
                    <div class="mb-2">
                        <label class="form-label fw-semibold">View Control <span class="text-danger">*</span></label>
                        <select class="form-control param-viewcontrol" data-index="${index}" required>
                            ${_sel(viewControl, VIEW_CONTROLS)}
                        </select>
                    </div>
                    <div class="mb-2 param-datasource-section" data-index="${index}" style="display:${isSelect ? 'block' : 'none'};">
                        <label class="form-label fw-semibold">Data Source Query</label>
                        <textarea class="form-control param-datasource" data-index="${index}" rows="2"
                                  placeholder="SELECT id, name, parent_id FROM table">${_escape(ex.dependencyQuery || '')}</textarea>
                        <small class="text-muted">Returns: value, text, parent_column (optional for cascading)</small>
                    </div>
                    <div class="mb-2 param-depends-section" data-index="${index}" style="display:${isSelect ? 'block' : 'none'};">
                        <label class="form-label fw-semibold">Depends On</label>
                        <select class="form-control param-dependson" data-index="${index}" data-saved-value="${_escape(ex.dependsOn || '')}">
                            <option value="">-- None --</option>
                        </select>
                    </div>
                    <div class="mb-2">
                        <label class="form-label fw-semibold">Default Value</label>
                        <input type="text" class="form-control param-default"  data-index="${index}"
                               value="${_escape(ex.defaultValue || '')}" placeholder="Default value">
                    
                                <small class="text-muted text-text-info-emphasis">For Date type, you can use tokens like {firstdayoflastmonth}, {lastdayoflastmonth}, {firstdayoflastyear},{lastdayoflastyear}</small>
                    </div>
                    <div class="mb-2">
                        <label class="form-label fw-semibold">Position <span class="text-danger">*</span></label>
                        <input type="number" class="form-control param-position" data-index="${index}"
                               value="${param.position}" min="1" required>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input param-required" type="checkbox" data-index="${index}"
                               ${ex.isRequired !== false ? 'checked' : ''}>
                        <label class="form-check-label">Required</label>
                    </div>
                </div>
            </div>
        </div>`;
    }

    function _updateDependsOnOptions() {
        const selectParams = [];
        document.querySelectorAll('.param-viewcontrol').forEach(el => {
            const idx    = parseInt(el.getAttribute('data-index'));
            const nameEl = document.querySelector(`.param-name[data-index="${idx}"]`);
            if (nameEl && el.value === 'Select')
                selectParams.push({ index: idx, name: nameEl.value });
        });

        document.querySelectorAll('.param-dependson').forEach(el => {
            const currentIndex = parseInt(el.getAttribute('data-index'));
            const vcEl = document.querySelector(`.param-viewcontrol[data-index="${currentIndex}"]`);
            if (!vcEl || vcEl.value !== 'Select') return;

            const savedVal = el.getAttribute('data-saved-value') || '';
            const options  = selectParams
                .filter(p => p.index < currentIndex)
                .map(p => `<option value="${_escape(p.name)}" ${p.name === savedVal ? 'selected' : ''}>${_escape(p.name)}</option>`)
                .join('');
            el.innerHTML = `<option value="">-- None --</option>${options}`;
        });
    }

    function _bindViewControlChange(container) {
        container.querySelectorAll('.param-viewcontrol').forEach(select => {
            select.addEventListener('change', function () {
                const idx      = this.getAttribute('data-index');
                const dsSection  = container.querySelector(`.param-datasource-section[data-index="${idx}"]`);
                const depSection = container.querySelector(`.param-depends-section[data-index="${idx}"]`);
                const isSelect   = this.value === 'Select';
                if (dsSection)  dsSection.style.display  = isSelect ? 'block' : 'none';
                if (depSection) depSection.style.display = isSelect ? 'block' : 'none';
                if (isSelect) _updateDependsOnOptions();
            });
        });
    }

    // ── Public API ───────────────────────────────────────────────────────────
    function display(parameters, isSP, onRemove) {
        const list = document.getElementById('parametersList');
        if (!list) return;

        if (parameters.length === 0) {
            list.innerHTML = '<div class="alert alert-info"><i class="fas fa-info-circle me-2"></i>No parameters found.</div>';
            return;
        }

        list.innerHTML = '<div class="row g-3">' +
            parameters.map((p, i) => _buildCard(p, i, isSP)).join('') +
            '</div>';

        _bindViewControlChange(list);
        _updateDependsOnOptions();

        if (isSP && typeof onRemove === 'function') {
            list.querySelectorAll('.remove-param-btn').forEach(btn => {
                btn.addEventListener('click', function () {
                    onRemove(parseInt(this.getAttribute('data-index')));
                });
            });
        }
    }

    function collect() {
        const params = {};
        document.querySelectorAll('#parametersList .card').forEach(card => {
            const nameEl = card.querySelector('.param-name');
            if (!nameEl) return;
            params[nameEl.value] = {
                id:              card.querySelector('.param-id')?.value            || null,
                description:     card.querySelector('.param-description')?.value   || '',
                type:            card.querySelector('.param-type')?.value          || 'Varchar2',
                viewControl:     card.querySelector('.param-viewcontrol')?.value   || 'TextBox',
                defaultValue:    card.querySelector('.param-default')?.value       || '',
                isRequired:      card.querySelector('.param-required')?.checked    ?? false,
                position:        parseInt(card.querySelector('.param-position')?.value) || 0,
                dependsOn:       card.querySelector('.param-dependson')?.value     || '',
                dependencyQuery: card.querySelector('.param-datasource')?.value    || '',
                direction:       card.querySelector('.param-direction')?.value     || 'Input',
            };
        });
        return params;
    }

    function collectAsArray() {
        const result = [];
        document.querySelectorAll('#parametersList .card').forEach((card, index) => {
            const nameEl = card.querySelector('.param-name');
            if (!nameEl) return;
            const type = card.querySelector('.param-type')?.value || 'Varchar2';
            if (type === 'RefCursor') return;
            result.push({
                Id:              card.querySelector('.param-id')?.value            || null,
                Name:            nameEl.value,
                Description:     card.querySelector('.param-description')?.value   || null,
                Type:            type,
                ViewControl:     card.querySelector('.param-viewcontrol')?.value   || 'TextBox',
                DefaultValue:    card.querySelector('.param-default')?.value       || null,
                IsRequired:      card.querySelector('.param-required')?.checked    ?? false,
                Position:        parseInt(card.querySelector('.param-position')?.value) || (index + 1),
                DependsOn:       card.querySelector('.param-dependson')?.value     || null,
                DependencyQuery: card.querySelector('.param-datasource')?.value    || null,
                Direction:       card.querySelector('.param-direction')?.value     || 'Input',
            });
        });
        return result;
    }

    function extractFromQuery(query) {
        const regex  = /:(\w+)/g;
        const seen   = new Set();
        const params = [];
        let match;
        while ((match = regex.exec(query)) !== null) {
            if (!seen.has(match[1])) {
                seen.add(match[1]);
                params.push({ name: match[1], position: params.length + 1 });
            }
        }
        return params;
    }

    function seedFromDOM() {
        const params = [];
        document.querySelectorAll('#parametersList .card').forEach((card, index) => {
            const nameEl = card.querySelector('.param-name');
            if (!nameEl) return;
            params.push({
                name:     nameEl.value,
                position: parseInt(card.querySelector('.param-position')?.value) || index + 1,
                existing: {
                    id:              card.querySelector('.param-id')?.value                              || null,
                    description:     card.querySelector('.param-description')?.value                    || '',
                    type:            card.querySelector('.param-type')?.value                           || 'Varchar2',
                    viewControl:     card.querySelector('.param-viewcontrol')?.value                    || 'TextBox',
                    defaultValue:    card.querySelector('.param-default')?.value                        || '',
                    isRequired:      card.querySelector('.param-required')?.checked                     ?? false,
                    dependsOn:       card.querySelector('.param-dependson')?.getAttribute('data-saved-value') || '',
                    dependencyQuery: card.querySelector('.param-datasource')?.value                     || '',
                    direction:       card.querySelector('.param-direction')?.value                      || 'Input',
                }
            });
        });
        return params;
    }

    return { display, collect, collectAsArray, extractFromQuery, seedFromDOM };
})();
