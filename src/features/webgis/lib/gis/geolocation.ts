/** Device positions live only in this in-memory controller. No transport or storage. */
export const LOCATION_STALE_AFTER_MS = 60_000;
export const LOW_ACCURACY_METERS = 100;

export interface DeviceLocation {
  latitude: number;
  longitude: number;
  accuracy: number;
  timestamp: number;
}

export interface DeviceLocationSnapshot {
  status: "idle" | "searching" | "active" | "stale" | "error";
  position: DeviceLocation | null;
  following: boolean;
  watching: boolean;
  message: string;
}

type LocationProvider = Pick<Geolocation, "watchPosition" | "clearWatch">;
interface LocationEnvironment {
  secure: boolean;
  geolocation?: LocationProvider;
}

interface ControllerOptions {
  getEnvironment?: () => LocationEnvironment;
  now?: () => number;
}

export const INITIAL_DEVICE_LOCATION: DeviceLocationSnapshot = {
  status: "idle",
  position: null,
  following: false,
  watching: false,
  message: "Lokasi belum diaktifkan.",
};

function browserEnvironment(): LocationEnvironment {
  return {
    secure: typeof window !== "undefined" && window.isSecureContext,
    geolocation: typeof navigator !== "undefined" ? navigator.geolocation : undefined,
  };
}

export function isLowAccuracy(position: DeviceLocation): boolean {
  return position.accuracy > LOW_ACCURACY_METERS;
}

export function formatLocationCoordinate(value: number): string {
  return value.toFixed(6);
}

export function formatLocationAccuracy(value: number): string {
  return `${value.toFixed(2)} m`;
}

function readPosition(position: GeolocationPosition, now: number): DeviceLocation | null {
  const { latitude, longitude, accuracy } = position.coords;
  if (
    !Number.isFinite(latitude) || Math.abs(latitude) > 90 ||
    !Number.isFinite(longitude) || Math.abs(longitude) > 180 ||
    !Number.isFinite(accuracy) || accuracy < 0 ||
    !Number.isFinite(position.timestamp) || position.timestamp <= 0 ||
    position.timestamp > now + LOCATION_STALE_AFTER_MS
  ) return null;

  // Deliberately exclude altitude, heading, speed, and the browser position object.
  return { latitude, longitude, accuracy, timestamp: position.timestamp };
}

function errorMessage(code: number): string {
  if (code === 1) return "Izin lokasi ditolak. Izinkan lokasi melalui pengaturan browser, lalu coba lagi.";
  if (code === 2) return "Lokasi tidak tersedia. Periksa layanan lokasi perangkat dan sinyal, lalu coba lagi.";
  if (code === 3) return "Waktu pencarian lokasi habis. Pastikan layanan lokasi aktif, lalu coba lagi.";
  return "Pembaruan lokasi gagal. Periksa izin dan layanan lokasi perangkat, lalu coba lagi.";
}

export function createDeviceLocationController(options: ControllerOptions = {}) {
  const getEnvironment = options.getEnvironment ?? browserEnvironment;
  const now = options.now ?? Date.now;
  const listeners = new Set<() => void>();
  let snapshot = INITIAL_DEVICE_LOCATION;
  let provider: LocationProvider | undefined;
  let watchId: number | null = null;
  let staleTimer: ReturnType<typeof setTimeout> | undefined;
  // Every cancellation invalidates callbacks, including callbacks from a previous retry.
  let session = 0;

  function publish(next: DeviceLocationSnapshot) {
    snapshot = next;
    listeners.forEach((listener) => listener());
  }

  function clearTimer() {
    if (staleTimer !== undefined) clearTimeout(staleTimer);
    staleTimer = undefined;
  }

  function cancelWatch() {
    session += 1;
    clearTimer();
    if (watchId !== null) provider?.clearWatch(watchId);
    watchId = null;
    provider = undefined;
  }

  function fail(message: string) {
    cancelWatch();
    publish({ status: "error", position: null, watching: false, following: false, message });
  }

  function start() {
    // No browser API is read during construction, SSR, subscription, or rendering.
    if (snapshot.watching) return;
    let environment: LocationEnvironment;
    try {
      environment = getEnvironment();
    } catch {
      fail("Lokasi tidak dapat diakses. Periksa izin lokasi browser, lalu coba lagi.");
      return;
    }
    if (!environment.secure) {
      fail("Lokasi memerlukan koneksi aman. Buka portal melalui HTTPS atau localhost.");
      return;
    }
    if (!environment.geolocation) {
      fail("Browser ini tidak mendukung Geolocation. Gunakan browser yang mendukung lokasi perangkat.");
      return;
    }

    cancelWatch();
    const currentSession = session;
    const currentProvider = environment.geolocation;
    provider = currentProvider;
    publish({ status: "searching", position: null, following: true, watching: true, message: "Mencari lokasi…" });
    try {
      const id = currentProvider.watchPosition(
        (received) => {
          if (session !== currentSession) return;
          const position = readPosition(received, now());
          if (!position) {
            fail("Koordinat lokasi tidak valid. Aktifkan kembali layanan lokasi perangkat, lalu coba lagi.");
            return;
          }
          // A delayed older fix must not move the marker backwards or make it look fresh.
          if (snapshot.position && position.timestamp < snapshot.position.timestamp) return;
          clearTimer();
          const remaining = LOCATION_STALE_AFTER_MS - (now() - position.timestamp);
          const stale = remaining <= 0;
          publish({
            status: stale ? "stale" : "active",
            position,
            following: snapshot.following,
            watching: true,
            message: stale
              ? "Pembaruan lokasi terhenti. Posisi terakhir sudah lebih dari 60 detik. Coba lagi."
              : isLowAccuracy(position) ? "Akurasi rendah" : "Lokasi aktif",
          });
          if (!stale) {
            staleTimer = setTimeout(() => {
              if (session !== currentSession || snapshot.position !== position) return;
              staleTimer = undefined;
              publish({
                ...snapshot,
                status: "stale",
                message: "Pembaruan lokasi terhenti. Posisi terakhir sudah lebih dari 60 detik. Coba lagi.",
              });
            }, remaining);
          }
        },
        (error) => {
          if (session !== currentSession) return;
          // Unavailable/timeout fixes are transient: hide the old position but
          // keep the one watcher so the browser can deliver its next valid fix.
          // Permission denial is terminal and must cancel the watch.
          if (error.code === 2 || error.code === 3) {
            clearTimer();
            publish({ ...snapshot, status: "error", position: null, message: errorMessage(error.code) });
          } else fail(errorMessage(error.code));
        },
        { enableHighAccuracy: true, maximumAge: 10_000, timeout: 20_000 },
      );
      // A mock/provider can synchronously call the error callback before returning its ID.
      if (session !== currentSession) currentProvider.clearWatch(id);
      else watchId = id;
    } catch {
      if (session === currentSession) fail("Lokasi tidak dapat dimulai. Periksa izin lokasi browser, lalu coba lagi.");
    }
  }

  function stop() {
    cancelWatch();
    publish({ ...INITIAL_DEVICE_LOCATION, message: "Lokasi dihentikan" });
  }

  function retry() {
    stop();
    start();
  }

  function setFollowing(following: boolean) {
    if (!snapshot.watching || snapshot.following === following) return;
    publish({ ...snapshot, following });
  }

  return {
    getSnapshot: () => snapshot,
    getServerSnapshot: () => INITIAL_DEVICE_LOCATION,
    subscribe(listener: () => void) {
      listeners.add(listener);
      return () => { listeners.delete(listener); };
    },
    start,
    stop,
    retry,
    setFollowing,
    dispose() {
      listeners.clear();
      stop();
    },
  };
}
