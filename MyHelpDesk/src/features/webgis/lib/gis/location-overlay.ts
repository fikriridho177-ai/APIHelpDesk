import type { Feature, Polygon } from "geojson";
import type { GeoJSONSource, Map as LibreMap } from "maplibre-gl";
import type { DeviceLocation } from "./geolocation";

// Kept separate from the catalog's `gis-*` namespace and never sent to a server.
export const LOCATION_SOURCE = "device-location-accuracy";
const fillId = "device-location-accuracy-fill";
const outlineId = "device-location-accuracy-outline";
const earthRadius = 6_371_008.8;

/** A geodesic circle, not a fixed pixel radius (which changes meaning with zoom). */
export function accuracyPolygon(position: DeviceLocation): Feature<Polygon> {
  const latitude = position.latitude * Math.PI / 180;
  const longitude = position.longitude * Math.PI / 180;
  const distance = position.accuracy / earthRadius;
  const ring: number[][] = [];
  for (let index = 0; index < 64; index++) {
    const bearing = index * 2 * Math.PI / 64;
    const lat = Math.asin(Math.max(-1, Math.min(1,
      Math.sin(latitude) * Math.cos(distance) + Math.cos(latitude) * Math.sin(distance) * Math.cos(bearing),
    )));
    const lng = longitude + Math.atan2(
      Math.sin(bearing) * Math.sin(distance) * Math.cos(latitude),
      Math.cos(distance) - Math.sin(latitude) * Math.sin(lat),
    );
    // Keep longitudes near the center to avoid a world-spanning dateline polygon.
    ring.push([lng * 180 / Math.PI, lat * 180 / Math.PI]);
  }
  ring.push([...ring[0]]);
  return { type: "Feature", properties: {}, geometry: { type: "Polygon", coordinates: [ring] } };
}

export function removeLocationAccuracy(map: LibreMap) {
  // Parent map cleanup may run before this component's cleanup.
  if (!map.getStyle()) return;
  for (const id of [outlineId, fillId]) if (map.getLayer(id)) map.removeLayer(id);
  if (map.getSource(LOCATION_SOURCE)) map.removeSource(LOCATION_SOURCE);
}

export function updateLocationAccuracy(map: LibreMap, position: DeviceLocation) {
  if (!map.getStyle()) return;
  const data = accuracyPolygon(position);
  const existing = map.getSource(LOCATION_SOURCE) as GeoJSONSource | undefined;
  if (existing) existing.setData(data);
  else map.addSource(LOCATION_SOURCE, { type: "geojson", data });
  if (!map.getLayer(fillId)) map.addLayer({
    id: fillId, type: "fill", source: LOCATION_SOURCE,
    paint: { "fill-color": "#2076c7", "fill-opacity": 0.12 },
  });
  if (!map.getLayer(outlineId)) map.addLayer({
    id: outlineId, type: "line", source: LOCATION_SOURCE,
    paint: { "line-color": "#2076c7", "line-opacity": 0.65, "line-width": 1.5 },
  });
}
