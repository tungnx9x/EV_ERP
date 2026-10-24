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

// ══════════════════════════════════════════════════════════════════
// Numeric inputs of the purchase-price calc popups (#calcModal on the
// Quotation form and SO Detail). One convention for every field:
//   • "," = thousand separator, "." = decimal point (12,500.75)
//   • arithmetic expressions allowed (1,200*3+50.5) — evaluated on blur
// Mark an input with data-calc-num and call wireCalcNumberInputs() once.
// ══════════════════════════════════════════════════════════════════

// Whitelist evaluator: NaN unless the string is only digits, dots, + - * / ( ) and spaces.
function evalCalcArithmetic(str) {
    if (!/^[\d.+\-*/()\s]+$/.test(str)) return NaN;
    try {
        var r = Function('"use strict"; return (' + str + ')')();
        return isFinite(r) ? r : NaN;
    } catch (e) { return NaN; }
}

// "12,500.75" → 12500.75 ; "1,200*3+50.5" → 3650.5 ; "" / invalid → 0
function parseCalcNumber(s) {
    if (s === null || s === undefined) return 0;
    if (typeof s === 'number') return isFinite(s) ? s : 0;
    var str = String(s).replace(/[,\s]/g, '');
    if (str === '') return 0;
    if (!/[+\-*/()]/.test(str)) {
        var n = parseFloat(str);
        return isNaN(n) ? 0 : n;
    }
    var r = evalCalcArithmetic(str);
    return isNaN(r) ? 0 : r;
}

// 12500.75 → "12,500.75" (no trailing zeros); 0 / empty → ''
function formatCalcNumber(n, maxDecimals) {
    if (n === null || n === undefined || n === '' || isNaN(n) || Number(n) === 0) return '';
    return Number(n).toLocaleString('en-US', { maximumFractionDigits: maxDecimals == null ? 4 : maxDecimals });
}

// Live-format while typing: group the integer part, keep the typed decimal part,
// preserve the caret. Skipped mid-expression (evaluated on blur instead).
function formatCalcNumberInput(input) {
    var oldVal = input.value;
    if (/[+\-*/()]/.test(oldVal)) return;
    var caret = input.selectionStart;
    if (caret === null || caret === undefined) caret = oldVal.length;

    // Count value chars (everything except grouping commas) left of the caret, so the caret
    // survives comma changes AND a just-typed trailing dot.
    var keepLeft = 0;
    for (var i = 0; i < caret; i++) {
        if (oldVal.charAt(i) !== ',') keepLeft++;
    }

    // Keep digits and the FIRST dot only.
    var cleaned = oldVal.replace(/,/g, '');
    var firstDot = cleaned.indexOf('.');
    if (firstDot !== -1) {
        cleaned = cleaned.slice(0, firstDot + 1) + cleaned.slice(firstDot + 1).replace(/\./g, '');
    }
    cleaned = cleaned.replace(/[^\d.]/g, '');
    if (cleaned === '') { input.value = ''; return; }

    var dotIdx = cleaned.indexOf('.');
    var hasDot = dotIdx !== -1;
    var intPart = hasDot ? cleaned.slice(0, dotIdx) : cleaned;
    var decPart = hasDot ? cleaned.slice(dotIdx + 1) : '';
    var groupedInt = intPart === '' ? (hasDot ? '0' : '') : parseInt(intPart, 10).toLocaleString('en-US');
    var formatted = groupedInt + (hasDot ? '.' + decPart : '');
    input.value = formatted;

    if (intPart === '' && hasDot) keepLeft++; // account for the auto-inserted leading 0
    var newPos = formatted.length, seen = 0;
    if (keepLeft <= 0) {
        newPos = 0;
    } else {
        for (var p = 0; p < formatted.length; p++) {
            if (formatted.charAt(p) !== ',') {
                seen++;
                if (seen === keepLeft) { newPos = p + 1; break; }
            }
        }
    }
    try { input.setSelectionRange(newPos, newPos); } catch (e) { /* non-text inputs */ }
}

// Blur: evaluate an expression (if any) and re-display the grouped result.
function commitCalcNumberInput(input) {
    var raw = (input.value || '').trim();
    if (raw === '') return;
    input.value = formatCalcNumber(parseCalcNumber(raw));
}

// Wire every [data-calc-num] input inside `root`; onChange runs after each edit (live recalc).
function wireCalcNumberInputs(root, onChange) {
    if (!root) return;
    root.querySelectorAll('input[data-calc-num]').forEach(function (el) {
        el.addEventListener('input', function () { formatCalcNumberInput(el); if (onChange) onChange(); });
        el.addEventListener('blur', function () { commitCalcNumberInput(el); if (onChange) onChange(); });
        el.addEventListener('focus', function () { el.select(); });
    });
}
