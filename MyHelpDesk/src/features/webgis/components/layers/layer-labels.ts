import type { GeometryType } from "../../lib/gis/types";
import { legendEntries, type VectorStyle } from "../../lib/gis/style";

export function geometryLabel(type: GeometryType) {
  if (type === "Polygon" || type === "MultiPolygon") return "Poligon";
  if (type === "LineString" || type === "MultiLineString") return "Garis";
  return "Titik";
}

/** Translate generated legend copy without changing the category values or colors. */
export function displayLegendEntries(style: VectorStyle, name: string) {
  return legendEntries(style, name).map((entry, index) => ({
    ...entry,
    label: style.mode === "categorized" && style.category
      ? index === style.category.categories.length
        ? "Nilai lainnya / kosong"
        : style.category.categories[index].value === "" ? "(teks kosong)" : entry.label
      : entry.label,
  }));
}
