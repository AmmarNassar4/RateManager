function removeRow(button) {
    const row = button.closest('tr');
    const table = row.closest('table');
    row.remove();
    renumberRows(table);
}

function renumberRows(table) {
    const rows = table.querySelectorAll('tbody tr');
    rows.forEach((row, rowIndex) => {
        row.querySelectorAll('input, select').forEach(input => {
            input.name = input.name.replace(/\[\d+\]/, `[${rowIndex}]`);
        });
    });
}

function filterRates() {
    const input = document.getElementById('filterInput');
    const table = document.getElementById('ratesTable');
    if (!input || !table) return;

    const term = input.value.toLowerCase();
    table.querySelectorAll('tbody tr').forEach(row => {
        const searchable = (row.getAttribute('data-search') || '').toLowerCase();
        row.style.display = searchable.includes(term) ? '' : 'none';
    });
}
