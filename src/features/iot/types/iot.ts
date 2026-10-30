
export type IoTSourceType = 'INTERNAL' | 'VENDOR'
export type IoTMonitoringStatus = 'ONLINE' | 'STALE' | 'NO_RECENT_DATA' | 'INACTIVE' | 'UNKNOWN'
export type IoTHistoryPeriod = 'day' | 'week' | 'month' | 'year'
export type IoTHistoryAggregation = 'sample' | 'hour' | 'day' | 'month'

export type IoTHistoryQuery =
    | { anchorDate?: string; from?: never; to?: never }
    | { anchorDate?: never; from: string; to: string }

export interface IoTDevice {
    loggerId: number
    loggerCode: string
    namaLogger: string
    sourceType: IoTSourceType
    lokasi: string | null
    latitude: number | null
    longitude: number | null
    controller: string | null
    communication: string | null
    intervalMenit: number
    aktif: boolean
}

export interface IoTDeviceListResponse {
    total: number
    data: IoTDevice[]
}

export interface IoTParameter {
    sensorParameterId: number
    sensorCode: string | null
    parameterCode: string | null
    namaParameter: string | null
    satuan: string | null
    categoryCode?: string | null
    categoryName?: string | null
    categoryOrder?: number | null
    kategoriCode?: string | null
    kategoriNama?: string | null
    kategoriUrutan?: number | null
}

export interface IoTParameterResponse {
    loggerId: number
    parameters: IoTParameter[]
}

export type SensorValue = number | string | null

export interface IoTReading {
    rawId: number
    loggerId: number
    ts: string
    receivedAt: string
    data: Record<string, SensorValue>
    errors?: unknown
}

export interface IoTHistoryResponse {
    loggerId: number
    total: number
    data: IoTReading[]
    period?: IoTHistoryPeriod
    aggregation?: IoTHistoryAggregation
    from?: string
    to?: string
    rawTotal?: number
}

export interface IoTDeviceWithLatest extends IoTDevice {
    latest: IoTReading | null
    readingError?: boolean
}

export interface IoTMonitoringDevice {
    loggerId: number
    loggerCode: string | null
    namaLogger: string | null
    sourceType: IoTSourceType | string | null
    lokasi: string | null
    latitude: number | null
    longitude: number | null
    intervalMenit: number
    aktif: boolean
    status: IoTMonitoringStatus | string
    rawId: number | null
    lastSensorAt: string | null
    lastReceivedAt: string | null
    ageMinutes: number | null
    data: Record<string, number | null>
    errors?: Record<string, string>
}

export interface IoTMonitoringResponse {
    total: number
    online: number
    stale: number
    noRecentData: number
    inactive: number
    devices: IoTMonitoringDevice[]
}
