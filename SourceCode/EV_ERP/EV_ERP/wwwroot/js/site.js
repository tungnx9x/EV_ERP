// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function escapeHtml(s) {
    return String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// Renders the JSON result of /Quotation/ImportFromExcel into the modal's result box.
// res.Errors (when present) is a list of "Dòng X, cột Y: ..." strings from the server.
function renderQuotationImportResult(resultDiv, res) {
    resultDiv.classList.remove('d-none');
    if (res.Success) {
        resultDiv.className = 'alert alert-success mt-3';
        resultDiv.innerHTML = escapeHtml(res.Message || 'Import thành công') +
            '<br/><a href="/Quotation/Detail/' + encodeURIComponent(res.Data.QuotationId) +
            '" class="btn btn-sm btn-primary mt-2"><i class="bi bi-eye me-1"></i>Xem báo giá</a>';
        return;
    }

    resultDiv.className = 'alert alert-danger mt-3';
    var html = '<div class="fw-semibold"><i class="bi bi-exclamation-triangle me-1"></i>' +
        escapeHtml(res.Message || 'Import thất bại').replace(/\n/g, '<br/>') + '</div>';
    if (res.Errors && res.Errors.length) {
        html += '<ol class="mb-0 mt-2 ps-3 small" style="max-height:260px;overflow-y:auto;">' +
            res.Errors.map(function (e) { return '<li class="mb-1">' + escapeHtml(e) + '</li>'; }).join('') +
            '</ol>';
    }
    resultDiv.innerHTML = html;
}
