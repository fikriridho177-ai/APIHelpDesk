
import { useEffect, useRef, useState } from "react";
import { Marker, type Map as LibreMap } from "maplibre-gl";
import { Icon } from "../../components/ui/icon";
import { removeLocationAccuracy, updateLocationAccuracy } from "../../lib/gis/location-overlay";
import { formatLocationAccuracy, formatLocationCoordinate, isLowAccuracy } from "../../lib/gis/geolocation";
import { useDeviceLocation } from "./use-device-location";
import type { MeasureMode } from "../../lib/gis/types";
import styles from "./device-location.module.css";

export function DeviceLocationControl({ map, interactionMode, fitRevision }: {
  map: LibreMap; interactionMode: MeasureMode; fitRevision?: number;
}) {
  const location = useDeviceLocation();
  const { position, status, following, watching, setFollowing } = location;
  const [open, setOpen] = useState(false);
  const trigger = useRef<HTMLButtonElement>(null);
  const panel = useRef<HTMLElement>(null);
  const firstFix = useRef(true);
  const livePosition = status === "active" ? position : null;

  useEffect(() => {
    // User movement (including keyboard and controls) releases automatic following.
    // Programmatic easeTo updates have no originalEvent.
    const manualMove = (event: { originalEvent?: unknown }) => {
      if (event.originalEvent) setFollowing(false);
    };
    const stopFollowing = () => setFollowing(false);
    map.on("movestart", manualMove);
    map.on("dragstart", stopFollowing);
    map.on("zoomstart", manualMove);
    return () => {
      map.off("movestart", manualMove);
      map.off("dragstart", stopFollowing);
      map.off("zoomstart", manualMove);
    };
  }, [map, setFollowing]);

  useEffect(() => {
    // Explicitly navigating to a GIS layer or measuring must not fight the GPS camera.
    setFollowing(false);
  }, [interactionMode, fitRevision, setFollowing]);

  useEffect(() => {
    if (!livePosition) {
      removeLocationAccuracy(map);
      return;
    }
    const element = document.createElement("div");
    element.className = `gis-device-location-dot ${styles.dot}`;
    element.setAttribute("role", "img");
    element.setAttribute("aria-label", "Posisi perangkat Anda");
    const marker = new Marker({ element })
      .setLngLat([livePosition.longitude, livePosition.latitude]).addTo(map);
    const drawAccuracy = () => updateLocationAccuracy(map, livePosition);
    drawAccuracy();
    map.on("style.load", drawAccuracy);
    return () => {
      map.off("style.load", drawAccuracy);
      marker.remove();
      removeLocationAccuracy(map);
    };
  }, [map, livePosition]);

  useEffect(() => {
    if (!livePosition || !following) return;
    const zoom = firstFix.current ? Math.min(16, Math.max(12, map.getZoom())) : map.getZoom();
    firstFix.current = false;
    // On phones the details panel spans the map center. Aim at the uncovered
    // area below it, without persisting padding on the shared GIS camera.
    let offsetY = 0;
    if (open && panel.current && window.matchMedia("(max-width: 760px)").matches) {
      const canvas = map.getCanvas().getBoundingClientRect();
      const panelBottom = panel.current.getBoundingClientRect().bottom - canvas.top;
      offsetY = Math.max(0, Math.min(canvas.height / 2 - 20, panelBottom / 2 - 15));
    }
    map.easeTo({
      center: [livePosition.longitude, livePosition.latitude], zoom,
      offset: [0, offsetY],
      duration: window.matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 400,
    });
    // Allow the requested first fix, then keep the drawing camera steady.
    if (interactionMode !== "none") setFollowing(false);
  }, [map, livePosition, following, interactionMode, setFollowing, open]);

  function start() {
    firstFix.current = true;
    setOpen(true);
    location.retry();
  }
  const label = status === "active"
    ? (position && isLowAccuracy(position) ? "Akurasi rendah" : "Lokasi aktif")
    : status === "searching" ? "Mencari lokasi…"
    : status === "stale" ? "Pembaruan lokasi terhenti"
    : status === "error" ? "Lokasi tidak tersedia" : "Lokasi dihentikan";

  return <>
    <button type="button" ref={trigger} className={`${styles.trigger} ${watching ? styles.active : ""}`}
      aria-label="Lokasi Saya" title="Lokasi Saya" aria-pressed={watching}
      aria-expanded={open} aria-controls="device-location-panel"
      onClick={() => {
        if (!watching && status === "idle") start();
        else setOpen(!open);
      }}>
      <Icon name="location" size={20} />
    </button>
    <section ref={panel} id="device-location-panel" hidden={!open} className={`gis-location-panel ${styles.panel}`}
      aria-labelledby="device-location-heading" onKeyDown={(event) => {
        if (event.key === "Escape") { event.stopPropagation(); setOpen(false); trigger.current?.focus(); }
      }}>
      <div className={styles.heading}>
        <h2 id="device-location-heading">Lokasi Saya</h2>
        <button type="button" className={styles.close} aria-label="Tutup panel lokasi"
          onClick={() => { setOpen(false); trigger.current?.focus(); }}><Icon name="x" size={16} /></button>
      </div>
      <p role="status" className={styles.status}>{label}</p>
      {location.message && location.message !== label && <p className={styles.message}>{location.message}</p>}
      {livePosition && <dl className={styles.coordinates}>
        <div><dt>Lintang</dt><dd>{formatLocationCoordinate(livePosition.latitude)}°</dd></div>
        <div><dt>Bujur</dt><dd>{formatLocationCoordinate(livePosition.longitude)}°</dd></div>
        <div><dt>Akurasi</dt><dd>{formatLocationAccuracy(livePosition.accuracy)}</dd></div>
      </dl>}
      <div className={styles.actions}>
        {livePosition && <button type="button" aria-pressed={following} disabled={interactionMode !== "none"}
          onClick={() => setFollowing(!following)}>Ikuti Lokasi</button>}
        {(status === "error" || status === "stale") && <button type="button" onClick={start}>Coba lagi lokasi</button>}
        {status === "idle" && <button type="button" onClick={start}>Aktifkan lokasi</button>}
        {(watching || position) && <button type="button" onClick={() => { map.stop(); location.stop(); }}>Hentikan Lokasi</button>}
      </div>
      {watching && interactionMode !== "none" && <p className={styles.message}>Pilih Jelajahi untuk mengikuti lokasi selama pelacakan.</p>}
      <p className={styles.privacy}>Hanya ditampilkan di perangkat ini. Pelacakan dapat terhenti saat layar mati atau tab tidak aktif.</p>
    </section>
  </>;
}
