/** Raster metadata shared with the UI. Storage identifiers stay on the server. */
export const managedRasterMarker = "portal-raster-v1";
export type RasterSourceType = "GEOTIFF" | "GEOPDF";
export interface RasterPage {
  page: number;
  width: number;
  height: number;
  bandCount: number;
  sourceSrid: number | null;
  sourceCrsWkt: string;
  bounds: [number, number, number, number];
  dataTypes: string[];
  noData: Array<number | null>;
}
export interface ProcessedRaster extends RasterPage {
  minZoom: number;
  maxZoom: number;
  outputBytes: number;
  tileCount: number;
  processedCrs: "EPSG:3857";
}
export interface RasterMetadata extends Partial<ProcessedRaster> {
  stage: "AWAITING_PAGE" | "READY";
  pages?: RasterPage[];
  selectedPage?: number;
  /** Private generation identifier; never return this field to a client. */
  artifactId?: string;
}
export function normalizeRasterStyle(input: unknown): { opacity: number } {
  const opacity = input && typeof input === "object" && "opacity" in input ? input.opacity : undefined;
  return { opacity: typeof opacity === "number" && Number.isFinite(opacity) ? Math.min(1, Math.max(0, opacity)) : 1 };
}
