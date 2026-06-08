(function () {
    // ── Invite form ──────────────────────────────────────────────────────────
    const statusEl = document.getElementById('invite-status');

    document.getElementById('invite-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        statusEl.textContent = 'Sending…';
        const body = { email: document.getElementById('invite-email').value };
        const resp = await fetch('/api/invitations', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (resp.ok) {
            const data = await resp.json();
            statusEl.innerHTML = '<span class="text-success">' + data.message + '</span>';
            setTimeout(() => location.reload(), 2000);
        } else {
            const text = await resp.text();
            statusEl.innerHTML = '<span class="text-danger">Failed: ' + text + '</span>';
        }
    });

    // ── Resend invite buttons ────────────────────────────────────────────────
    document.querySelectorAll('.resend-invite').forEach(btn => {
        btn.addEventListener('click', async () => {
            const email = btn.getAttribute('data-email');
            btn.disabled = true;
            btn.textContent = 'Sending…';
            const resp = await fetch('/api/invitations', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email }),
            });
            if (resp.ok) {
                btn.textContent = 'Sent';
                btn.classList.replace('btn-outline-secondary', 'btn-outline-success');
            } else {
                btn.disabled = false;
                btn.textContent = 'Resend Invite';
                const text = await resp.text();
                alert('Failed to resend: ' + text);
            }
        });
    });

    // ── Search + pagination ──────────────────────────────────────────────────
    const tbody = document.querySelector('#users-table tbody');
    const allRows = Array.from(tbody.querySelectorAll('tr'));
    const searchInput = document.getElementById('user-search');
    const pageSizeSelect = document.getElementById('page-size');
    const paginationEl = document.getElementById('pagination');
    const infoEl = document.getElementById('pagination-info');

    let currentPage = 1;
    let filteredRows = allRows.slice();

    function applyFilter() {
        const q = searchInput.value.trim().toLowerCase();
        filteredRows = allRows.filter(row =>
            !q ||
            row.dataset.name.includes(q) ||
            row.dataset.email.includes(q)
        );
        currentPage = 1;
        render();
    }

    function render() {
        const pageSize = parseInt(pageSizeSelect.value, 10);
        const totalPages = Math.max(1, Math.ceil(filteredRows.length / pageSize));
        if (currentPage > totalPages) currentPage = totalPages;

        const start = (currentPage - 1) * pageSize;
        const visible = new Set(filteredRows.slice(start, start + pageSize));

        allRows.forEach(row => {
            row.hidden = !visible.has(row);
        });

        // Info text
        const from = filteredRows.length === 0 ? 0 : start + 1;
        const to = Math.min(start + pageSize, filteredRows.length);
        infoEl.textContent = filteredRows.length === 0
            ? 'No users found'
            : `Showing ${from}–${to} of ${filteredRows.length} user${filteredRows.length !== 1 ? 's' : ''}`;

        // Pagination buttons
        paginationEl.innerHTML = '';

        const prevLi = document.createElement('li');
        prevLi.className = 'page-item' + (currentPage === 1 ? ' disabled' : '');
        prevLi.innerHTML = '<a class="page-link" href="#">&laquo;</a>';
        prevLi.querySelector('a').addEventListener('click', e => {
            e.preventDefault();
            if (currentPage > 1) { currentPage--; render(); }
        });
        paginationEl.appendChild(prevLi);

        // Show at most 5 page numbers centred around currentPage
        const delta = 2;
        const rangeStart = Math.max(1, currentPage - delta);
        const rangeEnd = Math.min(totalPages, currentPage + delta);

        if (rangeStart > 1) {
            appendPageBtn(1);
            if (rangeStart > 2) appendEllipsis();
        }
        for (let p = rangeStart; p <= rangeEnd; p++) appendPageBtn(p);
        if (rangeEnd < totalPages) {
            if (rangeEnd < totalPages - 1) appendEllipsis();
            appendPageBtn(totalPages);
        }

        const nextLi = document.createElement('li');
        nextLi.className = 'page-item' + (currentPage === totalPages ? ' disabled' : '');
        nextLi.innerHTML = '<a class="page-link" href="#">&raquo;</a>';
        nextLi.querySelector('a').addEventListener('click', e => {
            e.preventDefault();
            if (currentPage < totalPages) { currentPage++; render(); }
        });
        paginationEl.appendChild(nextLi);
    }

    function appendPageBtn(p) {
        const li = document.createElement('li');
        li.className = 'page-item' + (p === currentPage ? ' active' : '');
        const a = document.createElement('a');
        a.className = 'page-link';
        a.href = '#';
        a.textContent = p;
        a.addEventListener('click', e => {
            e.preventDefault();
            currentPage = p;
            render();
        });
        li.appendChild(a);
        paginationEl.appendChild(li);
    }

    function appendEllipsis() {
        const li = document.createElement('li');
        li.className = 'page-item disabled';
        li.innerHTML = '<span class="page-link">…</span>';
        paginationEl.appendChild(li);
    }

    searchInput.addEventListener('input', applyFilter);
    pageSizeSelect.addEventListener('change', () => { currentPage = 1; render(); });

    render();
})();
