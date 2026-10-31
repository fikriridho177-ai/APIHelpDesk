import type { Basemap } from "./types";
export const DEFAULT_BASEMAP_ID = "osm-topo";
// Curated configuration. The browser cannot submit tile URLs to the server.
export const basemaps: Basemap[] = [
  {
    id: "light",
    name: "Light canvas",
    description: "Private canvas · no external requests",
    background: "#e8eff0",
  },
  {
    id: "dark",
    name: "Dark canvas",
    description: "Private canvas · no external requests",
    background: "#182a37",
  },
  {
    id: "osm-topo",
    name: "OSM Topo",
    description: "Topographic map by OpenTopoMap · internet required",
    background: "#f5f3e8",
    tiles: ["https://a.tile.opentopomap.org/{z}/{x}/{y}.png"],
    attribution:
      'Map data: © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap contributors</a>, SRTM | Map style: © <a href="https://opentopomap.org" target="_blank" rel="noopener noreferrer">OpenTopoMap</a> (<a href="https://creativecommons.org/licenses/by-sa/3.0/" target="_blank" rel="noopener noreferrer">CC-BY-SA</a>)',
    maxZoom: 17,
  },
  {
    id: "osm",
    name: "OpenStreetMap",
    description: "External street map · internet required",
    background: "#e8eff0",
    tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"],
    attribution:
      '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener noreferrer">OpenStreetMap contributors</a>',
    maxZoom: 19,
  },
];
