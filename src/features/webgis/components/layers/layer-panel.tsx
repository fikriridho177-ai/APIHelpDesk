// Link admin mengarah ke aplikasi WebGIS asli (upload & style tetap di sana).
const Link = ({ href, children }: { href: string; children: React.ReactNode }) => <a href={href} target="_blank" rel="noreferrer">{children}</a>;
import { useState } from "react";
import { Icon } from "../../components/ui/icon";
import type { MapLayer } from "../../lib/gis/types";
import { LayerSymbol } from "./layer-symbol";
import { displayLegendEntries, geometryLabel } from "./layer-labels";
import styles from "./layer-panel.module.css";

export function LayerPanel({ layers, visible, onToggle, onFit, onResetVisibility, onOpacity, isAdmin = false, loading = false }: {
  layers: MapLayer[];
  visible: Record<string, boolean>;
  onToggle: (id: string) => void;
  onFit: (layer: MapLayer) => void;
  onResetVisibility: () => void;
  onOpacity?: (id: string, opacity: number) => void;
  isAdmin?: boolean;
  loading?: boolean;
}) {
  const [search, setSearch] = useState("");
  const [collapsed, setCollapsed] = useState<Record<string, boolean>>({});
  const query = search.trim().toLocaleLowerCase();
  const filtered = layers.filter((layer) => layer.name.toLocaleLowerCase().includes(query));
  const groups = new Map<string | null, MapLayer[]>();
  for (const layer of filtered) {
    const groupName = layer.groupName ?? null;
    const group = groups.get(groupName) ?? [];
    group.push(layer);
    groups.set(groupName, group);
  }
  return (
    <section className="gis-panel" aria-labelledby="layers-title" aria-busy={loading}>
      <div className="gis-panel-heading"><h2 id="layers-title" className="gis-panel-title"><Icon name="layers" size={18} />Layer</h2><span>{layers.length} tersedia</span></div>
      <label className={styles.search}>Cari layer<span className={styles.searchField}><Icon name="search" size={17} /><input type="search" value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Nama layer…" maxLength={200} /></span></label>
      <div className={styles.topActions}>
        <button type="button" className={styles.reset} disabled={!layers.length} onClick={onResetVisibility}><Icon name="refresh" size={15} />Tampilan default</button>
        {isAdmin && <Link href="/admin/layers"><Icon name="settings" size={15} />Kelola layer</Link>}
      </div>
      {loading ? <p className="gis-help" role="status">Memuat layer…</p> : layers.length === 0 ? <div className="gis-empty"><Icon name="layers" size={26} /><p>Belum ada layer GIS tersedia.</p><small>{isAdmin ? "Unggah data melalui menu Administrasi untuk mulai bekerja." : "Layer akan muncul setelah dipublikasikan administrator."}</small></div> : filtered.length === 0 ? <p className="gis-empty" role="status">Tidak ada layer yang cocok dengan pencarian.</p> : null}
      <div className={styles.groups}>
        {[...groups].map(([name, group]) => {
          const key = name === null ? "ungrouped" : `group:${name}`;
          const groupName = name ?? "Tanpa grup";
          const expanded = !collapsed[key] || Boolean(query);
          return <div key={key} className={styles.group}>
            <button type="button" className={styles.groupButton} aria-expanded={expanded} aria-label={`${expanded ? "Tutup" : "Buka"} grup ${groupName}`} onClick={() => { if (query) setSearch(""); setCollapsed((previous) => ({ ...previous, [key]: expanded })); }}>
              <Icon name={expanded ? "chevron-down" : "chevron-right"} size={16} /><Icon name="folder" size={16} /><span className={styles.groupName}>{groupName}</span><span className={styles.count}>{group.length}</span>
            </button>
            <div hidden={!expanded}>
              {group.map((layer) => <div key={layer.id} className={styles.entry} data-visible={visible[layer.id] ?? layer.defaultVisible}>
                <div className="gis-layer-item">
                  <label>
                    <input type="checkbox" aria-label={layer.name} checked={visible[layer.id] ?? layer.defaultVisible} onChange={() => onToggle(layer.id)} />
                    {layer.layerType === "RASTER" ? <span className={styles.rasterSymbol}><Icon name="map" size={22} /></span> : <LayerSymbol style={layer.style} />}
                    <span><strong title={layer.description || layer.name}>{layer.name}</strong><small>{layer.layerType === "RASTER" ? `${layer.sourceType === "GEOPDF" ? "GeoPDF" : "GeoTIFF"} · Raster` : `${geometryLabel(layer.geometryType)} · ${layer.featureCount.toLocaleString("id-ID")} fitur`}{!layer.isVisible ? " · Pratinjau admin" : ""}</small></span>
                  </label>
                  <button type="button" className="gis-icon-button" onClick={() => onFit(layer)} aria-label={`Zoom ke ${layer.name}`} title="Zoom ke layer"><Icon name="zoom-in" size={18} /></button>
                </div>
                <details className={styles.details}>
                  <summary aria-label={`Detail dan legenda ${layer.name}`}>Detail &amp; legenda</summary>
                  {layer.description && <p>{layer.description}</p>}
                  {layer.layerType === "RASTER" ? <>
                    <dl><dt>Sumber</dt><dd>{layer.sourceType === "GEOPDF" ? "GeoPDF" : "GeoTIFF"}</dd><dt>Dimensi sumber</dt><dd>{layer.raster.width.toLocaleString("id-ID")} × {layer.raster.height.toLocaleString("id-ID")} px</dd><dt>Band</dt><dd>{layer.raster.bandCount}</dd><dt>CRS sumber</dt><dd>{layer.raster.sourceSrid ? `EPSG:${layer.raster.sourceSrid}` : "CRS khusus"}</dd>{layer.raster.selectedPage !== undefined && layer.sourceType === "GEOPDF" && <><dt>Halaman PDF</dt><dd>{layer.raster.selectedPage}</dd></>}<dt>Zoom tile</dt><dd>{layer.raster.minZoom}–{layer.raster.maxZoom}</dd><dt>Tampilan default</dt><dd>{layer.defaultVisible ? "Tampil" : "Tersembunyi"}</dd></dl>
                    <p>Warna mengikuti gambar sumber. Legenda kategori dan atribut objek tidak tersedia.</p>
                    {onOpacity && <label className={styles.opacity}>Opasitas <span>{Math.round(layer.style.opacity * 100)}%</span><input type="range" min="0" max="100" step="1" value={Math.round(layer.style.opacity * 100)} aria-label={`Opasitas ${layer.name}`} onChange={(event) => onOpacity(layer.id, Number(event.target.value) / 100)} /></label>}
                  </> : <>
                    <dl><dt>Style</dt><dd>{layer.style.mode === "categorized" ? "Kategori" : "Simbol tunggal"}</dd><dt>Rentang zoom</dt><dd>{layer.style.minZoom}–{layer.style.maxZoom}</dd><dt>Label</dt><dd>{layer.style.label.enabled ? layer.style.label.field : "Nonaktif"}</dd><dt>Tampilan default</dt><dd>{layer.defaultVisible ? "Tampil" : "Tersembunyi"}</dd></dl>
                    <ul className={styles.legendList} aria-label={`Legenda ${layer.name}`}>
                      {displayLegendEntries(layer.style, layer.name).map((entry, index) => <li key={index}><LayerSymbol style={layer.style} color={entry.color} /><span>{entry.label}</span></li>)}
                    </ul>
                  </>}
                  {isAdmin && <div className={styles.adminLinks}>{layer.layerType !== "RASTER" && <Link href={`/admin/layers/${layer.id}/style`}><Icon name="sliders" size={15} />Edit style</Link>}<Link href={`/admin/layers#layer-${layer.id}`}><Icon name="edit" size={15} />{layer.layerType === "RASTER" ? "Kelola raster" : "Edit metadata"}</Link></div>}
                </details>
              </div>)}
            </div>
          </div>;
        })}
      </div>
      {layers.length > 0 && <p className={styles.notice}>Pilihan layer berlaku untuk sesi Anda. Pilih Tampilan default untuk mengikuti pengaturan administrator.</p>}
    </section>
  );
}
