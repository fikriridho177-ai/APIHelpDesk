import type { CurahHujanMonitorItem } from '../types'

export type MonitorRowWithMeta = CurahHujanMonitorItem & {
  NamaSensor?: string | null
}

export interface StationMeta {
  idSensor: string
  namaSensor: string
  titikIpal: string
  kodeWilayah: string
  latitude: string
  longitude: string
}

export function fallbackSensorName(sensorId: string): string {
  const numeric = Number(sensorId)
  return Number.isFinite(numeric)
    ? `LoggerClient${String(numeric).padStart(3, '0')}`
    : `Logger ${sensorId}`
}

export function filterRowsBySensor(
  rows: MonitorRowWithMeta[],
  sensorId: string,
): MonitorRowWithMeta[] {
  return rows.filter(
    row => String(row.IdSensor ?? '').trim() === sensorId,
  )
}

export function getStationMeta(
  rows: MonitorRowWithMeta[],
  sensorId: string,
): StationMeta {
  const filtered = filterRowsBySensor(rows, sensorId)
  const row = filtered.find(
    item => item.NamaSensor || item.TitikIpal || item.KodeWilayah,
  ) ?? filtered[0]

  return {
    idSensor: sensorId,
    namaSensor:
      String(row?.NamaSensor ?? '').trim() || fallbackSensorName(sensorId),
    titikIpal:
      String(row?.TitikIpal ?? '').trim() || 'Lokasi belum tersedia',
    kodeWilayah:
      String(row?.KodeWilayah ?? '').trim() || '-',
    latitude:
      row?.Latitude !== null && row?.Latitude !== undefined
        ? String(row.Latitude)
        : '-',
    longitude:
      row?.Longitude !== null && row?.Longitude !== undefined
        ? String(row.Longitude)
        : '-',
  }
}

export function stationUrl(pathname: string, sensorId: string): string {
  return `${pathname}?station=${encodeURIComponent(sensorId)}`
}
