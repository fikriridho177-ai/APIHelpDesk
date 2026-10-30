import * as XLSX from 'xlsx'

// Satu tabel = satu sheet Excel / satu tabel di PDF.
export type ExportTable = {
  title: string
  header: string[]
  rows: (string | number | null | undefined)[][]
}

function stamp() {
  return new Date().toISOString().slice(0, 10)
}

// Nama sheet Excel: maks 31 karakter, tanpa []:*?/\ dan tidak boleh dobel.
function sheetName(title: string, used: Set<string>) {
  const base = title.replace(/[[\]:*?/\\]/g, ' ').trim().slice(0, 28) || 'Sheet'
  let name = base
  for (let i = 2; used.has(name.toLowerCase()); i++) name = `${base.slice(0, 27)} ${i}`
  used.add(name.toLowerCase())
  return name
}

export function exportExcel(fileName: string, tables: ExportTable[]) {
  const book = XLSX.utils.book_new()
  const used = new Set<string>()

  tables.forEach(table => {
    const sheet = XLSX.utils.aoa_to_sheet([table.header, ...table.rows.map(row => row.map(value => value ?? ''))])
    sheet['!cols'] = table.header.map((head, index) => ({
      wch: Math.min(50, Math.max(head.length, ...table.rows.map(row => String(row[index] ?? '').length)) + 2),
    }))
    XLSX.utils.book_append_sheet(book, sheet, sheetName(table.title, used))
  })

  XLSX.writeFile(book, `${fileName}-${stamp()}.xlsx`)
}

// Font standar PDF tidak punya semua karakter Unicode: ganti tanda baca "pintar" dengan versi biasa.
function pdfText(value: unknown) {
  return String(value ?? '')
    .replace(/[–—]/g, '-')
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '"')
    .replace(/·/g, '-')
}

// PDF A4 landscape: judul, keterangan filter, lalu tabel-tabel. jsPDF dimuat saat dibutuhkan saja.
export async function exportPdf(fileName: string, title: string, subtitle: string, tables: ExportTable[]) {
  const [{ jsPDF }, autoTableModule] = await Promise.all([import('jspdf'), import('jspdf-autotable')])
  // jspdf-autotable berbentuk UMD: fungsi bisa ada di default, atau default.default (tergantung bundler).
  type AutoTable = typeof autoTableModule.default
  const mod = autoTableModule as unknown as { default: AutoTable | { default: AutoTable } }
  const autoTable: AutoTable = typeof mod.default === 'function' ? mod.default : mod.default.default

  const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' })
  const pageHeight = doc.internal.pageSize.getHeight()
  const pageWidth = doc.internal.pageSize.getWidth()

  doc.setFontSize(14)
  doc.text(pdfText(title), 14, 15)
  doc.setFontSize(9)
  doc.setTextColor(100)
  doc.text(pdfText(subtitle), 14, 21)
  doc.setTextColor(0)

  let y = 28
  tables.forEach(table => {
    if (y > pageHeight - 30) {
      doc.addPage()
      y = 15
    }
    doc.setFontSize(11)
    doc.text(pdfText(table.title), 14, y)

    autoTable(doc, {
      startY: y + 2,
      head: [table.header.map(pdfText)],
      body: table.rows.length > 0 ? table.rows.map(row => row.map(pdfText)) : [[{ content: 'Tidak ada data.', colSpan: table.header.length }]],
      theme: 'grid',
      styles: { fontSize: 7, cellPadding: 1.5, overflow: 'linebreak' },
      headStyles: { fillColor: [37, 99, 235], textColor: 255 },
      margin: { left: 14, right: 14 },
    })

    y = ((doc as unknown as { lastAutoTable?: { finalY: number } }).lastAutoTable?.finalY ?? y) + 10
  })

  // Nomor halaman.
  const pages = doc.getNumberOfPages()
  for (let page = 1; page <= pages; page++) {
    doc.setPage(page)
    doc.setFontSize(8)
    doc.setTextColor(120)
    doc.text(`Halaman ${page} / ${pages}`, pageWidth - 14, pageHeight - 8, { align: 'right' })
  }

  doc.save(`${fileName}-${stamp()}.pdf`)
}
