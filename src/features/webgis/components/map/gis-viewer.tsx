import { lazy, Suspense, useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { basemaps as basemapList } from "../../lib/gis/basemaps";
import { getGisLayers } from "../../webgisApi";
import { Icon } from "../../components/ui/icon";
import { LayerPanel } from "../../components/layers/layer-panel";
import { LegendPanel } from "../../components/layers/legend-panel";
import { parseVisibilityOverrides, visibilityStorageKey } from "../../components/layers/visibility-state";
import { validBounds } from "../../lib/gis/style";
import { DEFAULT_BASEMAP_ID } from "../../lib/gis/basemaps";
import { findPreferredGisLayer } from "../../lib/gis/layer-focus";
import { BasemapPanel } from "./basemap-panel";
import { MeasurementPanel } from "./measurement-panel";
import IoTMonitoringPanel from "../IoTMonitoringPanel";
import type { IoTMapMonitoringState } from "../../hooks/useIoTMapMonitoring";
import type { IoTMonitoringDevice } from "../../../iot/types/iot";
import { getIoTMapBounds } from "../../lib/iot-map";
import {
  formatArea,
  formatLength,
  lineMeters,
  polygonSquareMeters,
} from "../../lib/gis/measurement";
import type {
  AreaUnit,
  Basemap,
  Bounds,
  Coordinate,
  LengthUnit,
  MapLayer,
  MeasureMode,
} from "../../lib/gis/types";

// MapLibre cukup besar: dimuat saat halaman peta dibuka saja.
const MapCanvas = lazy(() => import("./map-canvas"));
const worldBounds: Bounds = [-170, -60, 170, 75];
const mobileQuery = "(max-width: 760px)";
function subscribeViewport(callback: () => void) {
  const media = window.matchMedia(mobileQuery);
  media.addEventListener("change", callback);
  return () => media.removeEventListener("change", callback);
}
const mobileSnapshot = () => window.matchMedia(mobileQuery).matches;
const serverSnapshot = () => false;
// preview = database WebGIS belum tersambung: peta dasar, ukur & lokasi tetap jalan, daftar layer kosong.
export function GISViewer({ userId, isAdmin, preview = false, iotMonitoring }: {
  userId: string;
  isAdmin: boolean;
  preview?: boolean;
  iotMonitoring: IoTMapMonitoringState;
}) {
  const [layers, setLayers] = useState<MapLayer[]>([]),
    [basemaps, setBasemaps] = useState<Basemap[]>([]);
  const [visible, setVisible] = useState<Record<string, boolean>>({}),
    [basemapId, setBasemapId] = useState(DEFAULT_BASEMAP_ID);
  const [loading, setLoading] = useState(true),
    [error, setError] = useState<string | null>(null),
    [retry, setRetry] = useState(0);
  const [sidebarPreference, setSidebarOpen] = useState<boolean | null>(null),
    [coordinate, setCoordinate] = useState<Coordinate | null>(null);
  const [iotPanelOpen, setIotPanelOpen] = useState(false);
  const mobile = useSyncExternalStore(subscribeViewport, mobileSnapshot, serverSnapshot);
  const sidebarOpen = sidebarPreference ?? !mobile;
  const sidebarToggle = useRef<HTMLButtonElement>(null);
  const sidebarClose = useRef<HTMLButtonElement>(null);
  const [mode, setMode] = useState<MeasureMode>("none"),
    [points, setPoints] = useState<Coordinate[]>([]),
    [finished, setFinished] = useState(false);
  const [lengthUnit, setLengthUnit] = useState<LengthUnit>("m"),
    [areaUnit, setAreaUnit] = useState<AreaUnit>("ha");
  const [fit, setFit] = useState<{ bounds: Bounds; revision: number }>({
    bounds: worldBounds,
    revision: 0,
  });
  const [revoked, setRevoked] = useState(false);
  const [catalogRevision, setCatalogRevision] = useState(0);
  const [opacity, setOpacity] = useState<Record<string, number>>({});
  const renderedLayers = useMemo(() => layers.map((layer) => layer.layerType === "RASTER" && opacity[layer.id] !== undefined
    ? { ...layer, style: { opacity: opacity[layer.id] } } : layer), [layers, opacity]);
  const selectedLayerApplied = useRef(false);
  const storageKey = visibilityStorageKey(userId);
  const iotDevices: IoTMonitoringDevice[] = iotMonitoring.canRead
    ? (iotMonitoring.monitoring?.devices ?? [])
    : [];
  const selectIoTLogger = useCallback((loggerId: number) => {
    const device = iotDevices.find((item) => item.loggerId === loggerId);
    setIotPanelOpen(true);
    iotMonitoring.selectLogger(loggerId);
    const bounds = device ? getIoTMapBounds([device]) : null;
    if (bounds) setFit((previous) => ({ bounds, revision: previous.revision + 1 }));
  }, [iotDevices, iotMonitoring]);
  const toggleIotPanel = useCallback(() => {
    setIotPanelOpen((previous) => !previous);
  }, []);
  const iotPanelState = useMemo(() => ({
    ...iotMonitoring,
    selectLogger: selectIoTLogger,
  }), [iotMonitoring, selectIoTLogger]);
  const focusIoTDevices = useCallback(() => {
    const bounds = getIoTMapBounds(iotDevices);
    if (bounds) setFit((previous) => ({ bounds, revision: previous.revision + 1 }));
  }, [iotDevices]);
  // Di sistem kita: akses dicabut -> tampilkan pesan (hak diatur lewat Akses Menu).
  const unauthorized = useCallback(() => {
    setRevoked(true);
  }, []);
  useEffect(() => {
    if (sidebarOpen && mobile) sidebarClose.current?.focus();
  }, [sidebarOpen, mobile]);
  useEffect(() => {
    let stopped = false,
      active = false;
    const controller = new AbortController();
    async function load() {
      if (active || document.visibilityState !== "visible") return;
      active = true;
      try {
        // Katalog dari API kita; daftar basemap tetap (sama seperti /api/basemaps WebGIS).
        let catalog: { layers: MapLayer[] };
        try {
          catalog = { layers: preview ? [] : await getGisLayers() };
        } catch (err) {
          const status = (err as { response?: { status?: number; data?: { message?: string } } }).response;
          if (stopped) return;
          if (status?.status === 403) {
            unauthorized();
            return;
          }
          throw new Error(status?.data?.message ?? "Unavailable");
        }
        const maps = { basemaps: basemapList };
        if (stopped) return;
        if (!selectedLayerApplied.current) {
          selectedLayerApplied.current = true;
          let overrides: Record<string, boolean> = {};
          try {
            overrides = parseVisibilityOverrides(window.sessionStorage.getItem(storageKey));
          } catch {
            // Private browsing or storage restrictions should not prevent map use.
          }
          const selectedId = new URLSearchParams(window.location.search).get("layer");
          const selected = (catalog.layers as MapLayer[]).find((layer) => layer.id === selectedId);
          const preferredLayer = selected ? null : findPreferredGisLayer(catalog.layers);
          if (selected) {
            // An explicit local preference survives refresh, including a deep link.
            if (overrides[selected.id] === undefined) overrides[selected.id] = true;
            if (validBounds(selected.bounds)) {
              setFit((previous) => ({ bounds: selected.bounds, revision: previous.revision + 1 }));
            } else {
              setError("Batas wilayah layer ini belum tersedia untuk ditampilkan.");
            }
          } else if (preferredLayer) {
            if (overrides[preferredLayer.id] === undefined) overrides[preferredLayer.id] = true;
            if (overrides[preferredLayer.id] !== false && validBounds(preferredLayer.bounds)) {
              setFit((previous) => ({ bounds: preferredLayer.bounds, revision: previous.revision + 1 }));
            }
          }
          setVisible(overrides);
        }
        setLayers((previous) =>
          JSON.stringify(previous) === JSON.stringify(catalog.layers)
            ? previous
            : catalog.layers,
        );
        setBasemaps((previous) =>
          JSON.stringify(previous) === JSON.stringify(maps.basemaps)
            ? previous
            : maps.basemaps,
        );
      } catch (err) {
        if (!stopped)
          setError(
            err instanceof Error && err.message !== "Unavailable"
              ? err.message
              : "Data peta belum dapat diperbarui. Periksa koneksi lalu coba lagi.",
          );
      } finally {
        active = false;
        if (!stopped) setLoading(false);
      }
    }
    void load();
    const interval = setInterval(() => void load(), 30000);
    const refresh = () => void load();
    document.addEventListener("visibilitychange", refresh);
    return () => {
      stopped = true;
      controller.abort();
      clearInterval(interval);
      document.removeEventListener("visibilitychange", refresh);
    };
  }, [retry, catalogRevision, unauthorized, storageKey, preview]);
  function changeVisibility(next: Record<string, boolean>) {
    setVisible(next);
    try {
      window.sessionStorage.setItem(storageKey, JSON.stringify(next));
    } catch {
      // Visibility still works for the current page when browser storage is unavailable.
    }
  }
  function fitLayer(layer: MapLayer) {
    if (!validBounds(layer.bounds)) {
      setError("Batas wilayah layer ini belum tersedia untuk ditampilkan.");
      return;
    }
    setFit((previous) => ({ bounds: layer.bounds, revision: previous.revision + 1 }));
  }
  function resetView() {
    const bounds = layers.map((layer) => layer.bounds).filter(validBounds);
    setFit((previous) => ({
      bounds: bounds.length ? [
        Math.min(...bounds.map((item) => item[0])),
        Math.min(...bounds.map((item) => item[1])),
        Math.max(...bounds.map((item) => item[2])),
        Math.max(...bounds.map((item) => item[3])),
      ] : worldBounds,
      revision: previous.revision + 1,
    }));
  }
  function chooseMode(next: MeasureMode) {
    setMode(next);
    setPoints([]);
    setFinished(false);
    if (next !== "none") setSidebarOpen(!mobile);
  }
  function addPoint(point: Coordinate) {
    setPoints((previous) =>
      previous.length >= 200 ? previous : [...previous, point],
    );
  }
  const base = basemaps.find((item) => item.id === basemapId) ?? basemaps[0];
  const shown = renderedLayers.filter((layer) => visible[layer.id] ?? layer.defaultVisible);
  const measurement =
    mode === "polygon"
      ? formatArea(polygonSquareMeters(points), areaUnit)
      : formatLength(lineMeters(points), lengthUnit);
  if (revoked)
    return <div className="gis-map-loading" role="status">Anda tidak memiliki akses ke Peta GIS. Minta hak akses menu Peta GIS ke administrator.</div>;
  return (
    <div className={`gis-workspace ${sidebarOpen ? "" : "sidebar-collapsed"}`}>
      <div className="gis-toolbar">
        <button
          type="button"
          ref={sidebarToggle}
          className="gis-tool sidebar-toggle"
          aria-expanded={sidebarOpen}
          aria-controls="gis-sidebar"
          onClick={() => setSidebarOpen(!sidebarOpen)}
        >
          <Icon name="layers" size={18} />
          <span>{sidebarOpen ? "Tutup panel" : "Buka panel"}</span>
        </button>
        <div className="gis-toolbar-title">
          <h1>Peta perkebunan</h1>
          <span>Ruang kerja geospasial RSUP</span>
        </div>
        <div className="gis-tools" role="group" aria-label="Alat peta">
          <button
            type="button"
            className={`gis-tool ${mode === "none" ? "active" : ""}`}
            aria-label="Jelajahi"
            title="Jelajahi peta"
            aria-pressed={mode === "none"}
            onClick={() => chooseMode("none")}
          >
            <Icon name="map" size={17} /><span className="gis-tool-label">Jelajahi</span>
          </button>
          <button
            type="button"
            className={`gis-tool ${mode === "line" ? "active" : ""}`}
            aria-label="Ukur jarak"
            title="Ukur jarak"
            aria-pressed={mode === "line"}
            onClick={() => chooseMode("line")}
          >
            <Icon name="ruler" size={17} /><span className="gis-tool-label">Ukur jarak</span>
          </button>
          <button
            type="button"
            className={`gis-tool ${mode === "polygon" ? "active" : ""}`}
            aria-label="Ukur luas"
            title="Ukur luas"
            aria-pressed={mode === "polygon"}
            onClick={() => chooseMode("polygon")}
          >
            <Icon name="polygon" size={17} /><span className="gis-tool-label">Ukur luas</span>
          </button>
          <button
            type="button"
            className="gis-tool"
            aria-label="Semua layer"
            title="Zoom ke semua layer"
            onClick={resetView}
          >
            <Icon name="zoom-in" size={17} /><span className="gis-tool-label">Semua layer</span>
          </button>
          <button type="button" className="gis-tool" aria-label="Perbarui layer" title="Perbarui layer" onClick={() => { setError(null); setCatalogRevision((previous) => previous + 1); }}><Icon name="refresh" size={17} /><span className="gis-tool-label">Perbarui layer</span></button>
        </div>
      </div>
      <div className="gis-body">
        <aside
          id="gis-sidebar"
          className="gis-sidebar"
          aria-label="Panel GIS"
          hidden={!sidebarOpen}
          onKeyDown={(event) => {
            if (event.key === "Escape" && !event.defaultPrevented && !(event.target instanceof HTMLSelectElement)) {
              event.stopPropagation();
              setSidebarOpen(false);
              sidebarToggle.current?.focus();
            }
          }}
        >
          <div className="gis-sidebar-intro">
            <div className="gis-sidebar-heading">
              <span className="eyebrow">Data &amp; tampilan peta</span>
              <button type="button" ref={sidebarClose} className="gis-icon-button gis-sidebar-close" aria-label="Tutup panel GIS" onClick={() => { setSidebarOpen(false); sidebarToggle.current?.focus(); }}><Icon name="x" size={18} /></button>
            </div>
            <p>Pilih layer untuk ditampilkan. Klik objek vector untuk melihat atribut; gunakan detail layer untuk informasi raster.</p>
          </div>
          <IoTMonitoringPanel
            state={iotPanelState}
            onFocusDevices={focusIoTDevices}
            expanded={iotPanelOpen}
            onToggle={toggleIotPanel}
          />
          {mode !== "none" && <MeasurementPanel
            mode={mode}
            points={points}
            lengthUnit={lengthUnit}
            areaUnit={areaUnit}
            finished={finished}
            onLengthUnit={setLengthUnit}
            onAreaUnit={setAreaUnit}
            onUndo={() => setPoints((previous) => previous.slice(0, -1))}
            onClear={() => {
              setPoints([]);
              setFinished(false);
            }}
            onFinish={() => setFinished(!finished)}
            onRemove={(index) =>
              setPoints((previous) => previous.filter((_, i) => i !== index))
            }
          />}
          <LayerPanel
            layers={renderedLayers}
            visible={visible}
            isAdmin={isAdmin}
            loading={loading}
            onToggle={(id) => {
              const layer = layers.find((item) => item.id === id);
              if (layer) changeVisibility({ ...visible, [id]: !(visible[id] ?? layer.defaultVisible) });
            }}
            onResetVisibility={() => { changeVisibility({}); setOpacity({}); }}
            onOpacity={(id, value) => setOpacity((previous) => ({ ...previous, [id]: value }))}
            onFit={(layer) => {
              fitLayer(layer);
              if (mobile) {
                setSidebarOpen(false);
                sidebarToggle.current?.focus();
              }
            }}
          />
          <BasemapPanel
            basemaps={basemaps}
            selected={basemapId}
            onSelect={(id) => {
              setBasemapId(id);
              setError(null);
            }}
          />
          <LegendPanel layers={shown} />
          <div className="gis-sidebar-foot">
            WGS 84 · EPSG:4326
            <br />
            Tampilan layer tersimpan untuk sesi Anda.
          </div>
        </aside>
        <section className="gis-map-stage" aria-label="Ruang kerja peta">
          {base && (<Suspense fallback={<div className="gis-map-loading" role="status">Menyiapkan peta…</div>}>
            <MapCanvas
              key={retry}
              showDeviceLocation
              layers={renderedLayers}
              visible={visible}
              basemap={base}
              mode={mode}
              points={points}
              finished={finished}
              fit={fit}
              onPoint={addPoint}
              onMovePoint={(index, point) =>
                setPoints((previous) =>
                  previous.map((item, i) => (i === index ? point : item)),
                )
              }
              onCoordinates={setCoordinate}
              onError={setError}
              onUnauthorized={unauthorized}
              iotDevices={iotDevices}
              selectedIoTLoggerId={iotMonitoring.selectedLoggerId}
              onSelectIoTLogger={selectIoTLogger}
            /></Suspense>
          )}
          {loading && (
            <div className="gis-map-loading" role="status">
              Memuat ruang kerja GIS…
            </div>
          )}
          {error && (
            <div className="gis-map-notice" role="alert">
              <span>{error}</span>
              <button
                type="button"
                onClick={() => {
                  setError(null);
                  setRetry((previous) => previous + 1);
                }}
              >
                Coba lagi
              </button>
            </div>
          )}
          <div className="gis-map-label">
            <span className="badge-dot" />
            {shown.length} layer aktif
          </div>
          {mode !== "none" && (
            <div className="gis-measure-floating">
              <strong>
                {mode === "polygon" ? "Luas" : "Jarak"}: {measurement}
              </strong>
              <span>
                {points.length} / 200 titik ·{" "}
                {finished ? "Geser titik untuk menyesuaikan" : "Klik peta untuk menambah titik"}
              </span>
              {!sidebarOpen && <button type="button" className="gis-measure-settings" onClick={() => setSidebarOpen(true)}><Icon name="sliders" size={16} />Pengaturan pengukuran</button>}
            </div>
          )}
          <div className="gis-coordinate-bar">
            <span>
              {coordinate
                ? `Bujur ${coordinate[0].toFixed(5)}° · Lintang ${coordinate[1].toFixed(5)}°`
                : "Arahkan kursor ke peta untuk melihat koordinat"}
            </span>
            <span>WGS 84</span>
          </div>
        </section>
      </div>
    </div>
  );
}
