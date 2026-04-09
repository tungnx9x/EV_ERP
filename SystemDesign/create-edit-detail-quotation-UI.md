## UI Layout
---

### Input Section

Contains the following controls:

| Field           | Type              | Description                                      |
|-----------------|-------------------|--------------------------------------------------|
| Customer        | Dropdown          | Data loaded from a JSON config file              |
| Coefficient     | Input (number)    | A multiplier used in unit price calculation      |

Note: Quotation Date is Datetime now no need to choose, staff in charge is current user create quotation
---

### Product Table Section

Below the Input Section.

| # | Column Name          | Description / Behavior                                              |
|---|----------------------|----------------------------------------------------------------------|
| 1 | **STT / No.**        | Auto-increment index                                                 |
| 2 | **Request**          | Product name — Dropdown choose product, popup create if doesn't exists|
| 3 | **Proposal**         | Product description — editable input, auto fill after choose product  |
| 4 | **Product Image**    | Product image - auto fill after choose product                                |
| 5 | **Unit**             | User input — editable                                                |
| 6 | **Quantity**         | User input — editable                                                |
| 7 | **Unit Price**       | `Import Price / Coefficient + Shipping` — editable                  |
| 8 | **Amount (Excl. VAT)** | `Unit Price × Quantity` — editable                               |
| 9 | **VAT**              | Default **8%** — editable                                            |
|10 | **Amount (Incl. VAT)** | `Amount (Excl. VAT) + (Amount (Excl. VAT) × VAT)` — auto-calculated |
|11 | **Note**             | User input — editable                                                |
|12 | **Supplier**         | Vendor name or URL                                        |
|13 | **Import Price**     | most recent product price  — editable                       |
|14 | **Currency**         | Dropdown selection then exchange to VND if not VND                                                    |
|15 | **Shipping**         | User input — editable                                                |
|16 | **Coefficient**      | Defaults to the value from the input above — editable per row        |

---

### Export Section

Located below the table.

- **Button:** `Export Quotation File`
- **Action:** Generates an Excel file based on the template `quotation-template.xlsx` populated with table data.
- **Output filename format:**
  ```
  YYYY.MM.DD Quotation EVH-{customer name}-{product name}
  ```

---