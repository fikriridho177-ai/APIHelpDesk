# Audit Excel bisnis RNA/Sensus

Audit sebelum perubahan source: seluruh 20 sheet pada masing-masing workbook, formula, merge, status hidden, dan drawing diperiksa.

## Pola bersama

Satu transaksi SnsHeader/SnsDetail per jenis HPT dengan dimensi persil, wilayah, status lahan, jenis form dan periode. Workbook adalah output, bukan model tabel. KIN memiliki satu rekap/memo; W1 memisahkan output Organik/Non-Organik. Tidak ada kebutuhan tabel per wilayah/status/form.

## Rekap hama dan Penyakit IM KIN.xlsx

SHA-256: ffa5db7fface4e9e9b2e457970f2b0a99c818e1cb1edcfd3fb1c48e7c25a8842

| Sheet | Peran | Rentang | Formula rusak #REF! |
| --- | --- | --- | --- |
| Data Persil Lhn Organik-RA 2023 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:AM364 | 0 |
| Data Persil RA 2024 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | B1:N244 | 0 |
| RKP-LHN ORGANIK | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:U340 | 0 |
| IM | Internal Memo: header, rekap per HPT, rekomendasi, tanda tangan; bagian memo historis di bawah halaman bukan transaksi baru | A1:T115 | 0 |
| From 04, Rekap HP  | Rekap/output utama: persil, luas, tahun tanam, O/N/R, populasi, sampel, jalur, jumlah/persen per HPT, pohon mati, rekomendasi, pengendalian, musuh alami, cuaca | A1:BX231 | 30 |
| SERTKS 16-21=6.911,09 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:T160 | 0 |
| SERTKSI 22=2.661.11 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:T75 | 0 |
| KONVERSI 23=970.08 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:T40 | 0 |
| Sheet2 | Kosong/formatting saja, tidak dibuat menjadi tabel/output | F15:I17 | 0 |
| Data Persil RA 2023 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | B1:N329 | 0 |
| Sheet1 | Kosong/formatting saja, tidak dibuat menjadi tabel/output | kosong | 0 |
| Sheet3 | Kosong/formatting saja, tidak dibuat menjadi tabel/output | kosong | 0 |
| Data Persil Lhn Organik-RA 2025 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:AM608 | 8 |
| Peta RSUP | Referensi peta/gambar, bukan sumber transaksi atau master persil baru | A1:CR90 | 0 |
| Sheet4 | Referensi kebutuhan pupuk Urea/MOP; di luar transaksi/report Sensus HPT | B2:L57 | 0 |
| List persil organik 2025 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:AN306 | 0 |
| Sheet5 | Referensi/sketsa pola jalur dan pokok sampel | B2:AH20 | 0 |
| Lampiran | Referensi diagram pola sampel untuk serangan >5% / <5%; bukan rule evaluasi universal | A1:AC30 | 0 |
| Form 2Sensus pesial (Ulat Daun) | Input/reference Form 2 berupa gambar EMF: persil, jalur, pohon, spesies, telur, ulat kecil/besar/mati, pupa hidup/kosong/mati, catatan; bukan sheet kosong | kosong | 0 |
| LapRekomendasi HP | Output laporan rekomendasi; banyak referensi #REF! harus dihitung ulang dari APPROVED + rule DB | A1:BN240 | 1503 |

## Rekap Hama Penyakit IM Wilayah 1.xlsx

SHA-256: 39d107facd95cd72f04b68d2a5f152fcec5ed822149de975196ff6eae2183edd

| Sheet | Peran | Rentang | Formula rusak #REF! |
| --- | --- | --- | --- |
| Data Persil Lhn Organik-RA 2023 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:AM364 | 0 |
| Data Persil RA 2024 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | B1:N244 | 0 |
| RKP-LHN ORGANIK | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:U340 | 0 |
| IM organik | Internal Memo: header, rekap per HPT, rekomendasi, tanda tangan; bagian memo historis di bawah halaman bukan transaksi baru | A1:T117 | 0 |
| IM Non organik | Internal Memo: header, rekap per HPT, rekomendasi, tanda tangan; bagian memo historis di bawah halaman bukan transaksi baru | A1:T118 | 0 |
| Form Rekap Organik | Rekap/output utama: persil, luas, tahun tanam, O/N/R, populasi, sampel, jalur, jumlah/persen per HPT, pohon mati, rekomendasi, pengendalian, musuh alami, cuaca | A1:BX219 | 106 |
| Form Rekap Non-Organik | Rekap/output utama: persil, luas, tahun tanam, O/N/R, populasi, sampel, jalur, jumlah/persen per HPT, pohon mati, rekomendasi, pengendalian, musuh alami, cuaca | A1:BX223 | 172 |
| SERTKS 16-21=6.911,09 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:T160 | 0 |
| SERTKSI 22=2.661.11 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:T75 | 0 |
| KONVERSI 23=970.08 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:T40 | 0 |
| Sheet2 | Kosong/formatting saja, tidak dibuat menjadi tabel/output | F15:I17 | 0 |
| Data Persil RA 2023 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | B1:N329 | 0 |
| Sheet1 | Kosong/formatting saja, tidak dibuat menjadi tabel/output | kosong | 0 |
| Sheet3 | Kosong/formatting saja, tidak dibuat menjadi tabel/output | kosong | 0 |
| Data Persil Lhn Organik-RA 2025 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:AM608 | 8 |
| Peta RSUP | Referensi peta/gambar, bukan sumber transaksi atau master persil baru | A1:CR90 | 0 |
| Sheet4 | Referensi kebutuhan pupuk Urea/MOP; di luar transaksi/report Sensus HPT | B2:L57 | 0 |
| List persil organik 2025 | Master/reference historis persil, populasi, luas, sertifikasi/konversi dan rencana pengendalian bulanan; reuse master persil existing, tidak diimpor sebagai transaksi | A1:AN306 | 0 |
| Sheet5 | Referensi/sketsa pola jalur dan pokok sampel | B2:AH20 | 0 |
| LapRekomendasi HP | Output laporan rekomendasi; banyak referensi #REF! harus dihitung ulang dari APPROVED + rule DB | A1:BN240 | 1444 |

## Temuan dan perbedaan

- Form 2 EMF ditemukan pada workbook KIN. Kode spesies ulat: SA, SN, DT, DD, UA, MC, MP, PP, UK. Form 2 Plesispa menggunakan parameter master existing; kolom khusus yang tidak dicontohkan tidak dikarang.
- Status lahan pada rekap memakai O/N/R. Header laporan harus dinamis dari filter; jangan menyalin nama W1/KIN atau periode tetap.
- Memo mengandung nomor/nama/tanggal historis dan memo lama kedua di bawah halaman. Output baru tidak menyalin nomor atau penanda tangan contoh sebagai fakta.
- Terdapat perbedaan ambang: memo menulis Oryctes TM >=3% dan Tirathaba >5%, sedangkan evaluasi existing >3% dan >=5%. Sambil menunggu keputusan bisnis, seed mempertahankan existing; seluruh rule dapat diubah admin.
- Formulasi Excel rusak dan beberapa kolom bantu menunjuk kolom HPT yang bergeser. Aplikasi menghitung jumlah/persen dari data APPROVED, tidak menyalin kesalahan referensi.
- Rekap satu persil dapat memuat beberapa HPT dengan denominator berbeda. Sampel/persen dihitung per HPT; denominator common yang tidak konsisten tidak dijumlahkan atau diasumsikan sama.

## Desain data

MstHpt dan tabel parameter existing MstParameterHpt dipertahankan (nama konsep bisnis: MstHptParameter). Tambahkan MstHptRule; transaksi tetap SnsHeader/SnsDetail/SnsFoto/SnsLog. Tambahkan snapshot status lahan/sumber, jenis form, konteks persil dan pengamatan umum pada header existing, serta scope parameter form pada master. Tidak ada tabel wilayah/organik/form baru.