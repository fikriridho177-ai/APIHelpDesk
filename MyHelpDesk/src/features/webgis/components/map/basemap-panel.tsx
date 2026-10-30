import type { Basemap } from "../../lib/gis/types";
const basemapLabels: Record<string, { name: string; description: string }> = {
  light: { name: "Kanvas terang", description: "Latar terang untuk menonjolkan layer" },
  dark: { name: "Kanvas gelap", description: "Latar gelap untuk membandingkan warna" },
  "osm-topo": { name: "OSM Topo", description: "Topografi OpenTopoMap · memerlukan internet" },
  osm: { name: "OpenStreetMap", description: "Peta jalan · memerlukan internet" },
};
export function BasemapPanel({
  basemaps,
  selected,
  onSelect,
}: {
  basemaps: Basemap[];
  selected: string;
  onSelect: (id: string) => void;
}) {
  return (
    <fieldset className="gis-panel gis-basemaps">
      <legend>Peta dasar</legend>
      {basemaps.map((basemap) => (
        <label
          key={basemap.id}
          className={selected === basemap.id ? "selected" : ""}
        >
          <input
            type="radio"
            name="basemap"
            value={basemap.id}
            checked={selected === basemap.id}
            onChange={() => onSelect(basemap.id)}
          />
          <span
            className={`gis-basemap-swatch ${basemap.tiles ? "streets" : ""}`}
            style={{ backgroundColor: basemap.background }}
            aria-hidden="true"
          />
          <span>
            <strong>{basemapLabels[basemap.id]?.name ?? basemap.name}</strong>
            <small>{basemapLabels[basemap.id]?.description ?? basemap.description}</small>
          </span>
        </label>
      ))}
    </fieldset>
  );
}
