import { Icon } from "../../components/ui/icon";
import type { MapLayer } from "../../lib/gis/types";
import { LayerSymbol } from "./layer-symbol";
import { displayLegendEntries } from "./layer-labels";
import styles from "./layer-panel.module.css";
export function LegendPanel({ layers }: { layers: MapLayer[] }) {
  return (
    <section className="gis-panel" aria-labelledby="legend-title">
      <h2 id="legend-title" className="gis-panel-title"><Icon name="info" size={18} />Legenda</h2>
      {layers.length ? <ul className={styles.legendLayers}>
        {layers.map((layer) => <li key={layer.id}>
          {layer.layerType === "RASTER" ? <>
            <strong className={styles.legendTitle}>{layer.name}</strong>
            <span className={styles.legendNote}>{layer.sourceType === "GEOPDF" ? "GeoPDF" : "GeoTIFF"} · warna dari gambar sumber. Legenda kategori tidak tersedia.</span>
          </> : <>
          {layer.style.mode === "categorized" && <><strong className={styles.legendTitle}>{layer.name}</strong><span className={styles.legendNote}>{layer.style.category?.field}</span></>}
          <ul className={styles.legendList} aria-label={`Legenda ${layer.name}`}>
            {displayLegendEntries(layer.style, layer.name).map((entry, index) => <li key={index}><LayerSymbol style={layer.style} color={entry.color} /><span>{entry.label}</span></li>)}
          </ul>
          {layer.style.label.enabled && <span className={styles.legendNote}>Label: {layer.style.label.field} · zoom {layer.style.label.minZoom}–{layer.style.label.maxZoom}</span>}
          </>}
        </li>)}
      </ul> : <p className="gis-help">Aktifkan layer untuk melihat legenda.</p>}
    </section>
  );
}
