(function () {
    const modal = document.getElementById('poc-modal');
    const titleEl = document.getElementById('poc-modal-title');
    const form = document.getElementById('poc-form');
    const idEl = document.getElementById('poc-id');
    const nameEl = document.getElementById('poc-name');
    const urlEl = document.getElementById('poc-url');
    const descEl = document.getElementById('poc-description');
    const allowAllEl = document.getElementById('allow-all-viewers');
    const userListEl = document.getElementById('user-list');
    const searchEl = document.getElementById('user-search');
    const unresolvedEl = document.getElementById('unresolved-warning');
    const aiBtn = document.getElementById('generate-ai');
    const aiStatusEl = document.getElementById('ai-status');

    function resetForm() {
        idEl.value = '';
        nameEl.value = '';
        urlEl.value = '';
        descEl.value = '';
        allowAllEl.checked = false;
        document.querySelectorAll('.poc-user').forEach(cb => cb.checked = false);
        unresolvedEl.classList.add('d-none');
        unresolvedEl.textContent = '';
        applyAllToggle();
    }

    function loadPoc(poc) {
        idEl.value = poc.id || poc.Id;
        nameEl.value = poc.name || poc.Name;
        urlEl.value = poc.url || poc.Url;
        descEl.value = poc.description || poc.Description;
        allowAllEl.checked = poc.allowAllViewers || poc.AllowAllViewers;

        const ids = poc.allowedUserIds || poc.AllowedUserIds || [];
        document.querySelectorAll('.poc-user').forEach(cb => {
            cb.checked = false;
        });
        // We map by email shown to admin; access list stores oids. Server returns access view by /access endpoint.
        if (idEl.value) {
            fetch('/api/pocs/' + idEl.value + '/access')
                .then(r => r.json())
                .then(view => {
                    const emails = new Set((view.users || []).map(u => (u.email || '').toLowerCase()));
                    document.querySelectorAll('.poc-user').forEach(cb => {
                        cb.checked = emails.has((cb.value || '').toLowerCase());
                    });
                });
        }
        applyAllToggle();
    }

    function applyAllToggle() {
        const disabled = allowAllEl.checked;
        document.querySelectorAll('.poc-user').forEach(cb => { cb.disabled = disabled; });
    }

    document.getElementById('add-poc-btn').addEventListener('click', () => {
        resetForm();
        titleEl.textContent = 'Add POC';
    });

    document.querySelectorAll('.edit-poc').forEach(btn => {
        btn.addEventListener('click', () => {
            const poc = JSON.parse(btn.getAttribute('data-poc'));
            resetForm();
            titleEl.textContent = 'Edit POC';
            loadPoc(poc);
            new bootstrap.Modal(modal).show();
        });
    });

    document.querySelectorAll('.delete-poc').forEach(btn => {
        btn.addEventListener('click', async () => {
            if (!confirm('Delete this POC?')) return;
            const id = btn.getAttribute('data-id');
            const resp = await fetch('/api/pocs/' + id, { method: 'DELETE' });
            if (resp.ok) location.reload();
            else alert('Delete failed: ' + resp.status);
        });
    });

    allowAllEl.addEventListener('change', applyAllToggle);

    searchEl.addEventListener('input', () => {
        const q = searchEl.value.toLowerCase();
        document.querySelectorAll('#user-list .user-row').forEach(row => {
            const text = (row.getAttribute('data-email') + ' ' + row.getAttribute('data-display')).toLowerCase();
            row.style.display = text.includes(q) ? '' : 'none';
        });
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();
        const id = idEl.value;
        const allowedEmails = Array.from(document.querySelectorAll('.poc-user'))
            .filter(cb => cb.checked && !cb.disabled)
            .map(cb => cb.value);
        const body = {
            name: nameEl.value,
            url: urlEl.value,
            description: descEl.value,
            allowAllViewers: allowAllEl.checked,
            allowedEmails: allowedEmails,
        };
        const resp = await fetch(id ? '/api/pocs/' + id : '/api/pocs', {
            method: id ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (!resp.ok) {
            const text = await resp.text();
            alert('Save failed: ' + text);
            return;
        }
        const result = await resp.json();
        if (result.unresolvedEmails && result.unresolvedEmails.length > 0) {
            unresolvedEl.classList.remove('d-none');
            unresolvedEl.textContent = 'Some emails could not be resolved: ' + result.unresolvedEmails.join(', ');
            setTimeout(() => location.reload(), 2000);
        } else {
            location.reload();
        }
    });

    if (aiBtn) {
        aiBtn.addEventListener('click', async () => {
            if (!nameEl.value) {
                aiStatusEl.textContent = 'Enter a name first';
                return;
            }
            aiBtn.disabled = true;
            aiStatusEl.textContent = 'Generating…';
            try {
                const resp = await fetch('/api/pocs/generate-description', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ name: nameEl.value, url: urlEl.value || null }),
                });
                if (!resp.ok) throw new Error('HTTP ' + resp.status);
                const data = await resp.json();
                descEl.value = data.description;
                aiStatusEl.textContent = 'AI draft — please review';
            } catch (err) {
                aiStatusEl.textContent = "Couldn't generate description, please write one manually";
            } finally {
                aiBtn.disabled = false;
            }
        });
    }
})();
