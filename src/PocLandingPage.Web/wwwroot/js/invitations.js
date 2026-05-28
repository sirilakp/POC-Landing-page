(function () {
    const statusEl = document.getElementById('invite-status');

    document.getElementById('invite-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        statusEl.textContent = 'Sending…';
        const body = {
            email: document.getElementById('invite-email').value,
            role: document.getElementById('invite-role').value,
        };
        const resp = await fetch('/api/invitations', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (resp.ok) {
            const data = await resp.json();
            statusEl.innerHTML = '<span class="text-success">' + data.message + '</span>';
            setTimeout(() => location.reload(), 1500);
        } else {
            const text = await resp.text();
            statusEl.innerHTML = '<span class="text-danger">Failed: ' + text + '</span>';
        }
    });

    document.querySelectorAll('.role-select').forEach(sel => {
        sel.addEventListener('change', async () => {
            const row = sel.closest('tr');
            const userId = row.getAttribute('data-user-id');
            const resp = await fetch('/api/invitations/' + userId + '/role', {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ role: sel.value }),
            });
            if (!resp.ok) alert('Role update failed');
        });
    });

    document.querySelectorAll('.revoke-user').forEach(btn => {
        btn.addEventListener('click', async () => {
            if (!confirm('Revoke access for this user?')) return;
            const id = btn.getAttribute('data-id');
            const resp = await fetch('/api/invitations/' + id, { method: 'DELETE' });
            if (resp.ok) location.reload();
            else alert('Revoke failed');
        });
    });
})();
