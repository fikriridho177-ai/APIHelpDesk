
import axios from 'axios'

import type {
    IoTDeviceListResponse,
    IoTMonitoringResponse,
    IoTParameterResponse,
    IoTHistoryResponse,
    IoTReading,
    IoTHistoryPeriod,
    IoTHistoryQuery,
    IoTSourceType,
} from '../types/iot'
import { copyAwsRainfallToAwl } from '../lib/rainfallSource'

const iotHttp = axios.create({
    baseURL:
        import.meta.env.VITE_IOT_API_URL ??
        `${window.location.protocol}//${window.location.hostname}:5000/api`,
    timeout: 15000,
})

iotHttp.interceptors.request.use(config => {
    const token = localStorage.getItem('token')

    if (token) {
        config.headers.Authorization = `Bearer ${token}`
    }

    return config
})

iotHttp.interceptors.response.use(
    response => response,
    error => {
        if (error.response?.status === 401) {
            localStorage.removeItem('token')
            localStorage.removeItem('auth')

            if (window.location.pathname !== '/login') {
                window.location.assign('/login')
            }
        }

        return Promise.reject(error)
    },
)

export async function getIoTDevices(
    sourceType?: IoTSourceType,
): Promise<IoTDeviceListResponse> {
    const { data } = await iotHttp.get<IoTDeviceListResponse>(
        '/iot/devices',
        {
            params: sourceType ? { sourceType } : {},
        },
    )

    return data
}

export async function getIoTMonitoring(): Promise<IoTMonitoringResponse> {
    const { data } = await iotHttp.get<IoTMonitoringResponse>('/iot/monitoring')
    return copyAwsRainfallToAwl(data)
}

export async function getIoTLatest(
    loggerId: number,
): Promise<IoTReading | null> {
    const { data } = await iotHttp.get<IoTReading | null>(
        `/iot/devices/${loggerId}/latest`,
    )

    return data
}

export async function getIoTParameters(
    loggerId: number,
): Promise<IoTParameterResponse> {
    const { data } = await iotHttp.get<IoTParameterResponse>(
        `/iot/devices/${loggerId}/parameters`,
    )

    return data
}

export async function getIoTHistory(
    loggerId: number,
    period: IoTHistoryPeriod,
    query: IoTHistoryQuery | string = {},
): Promise<IoTHistoryResponse> {
    const historyQuery: IoTHistoryQuery = typeof query === 'string'
        ? { anchorDate: query }
        : query

    const { data } = await iotHttp.get<IoTHistoryResponse>(
        `/iot/devices/${loggerId}/history`,
        {
            params: {
                period,
                ...historyQuery,
            },
        },
    )

    return data
}
